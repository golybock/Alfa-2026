using System.Globalization;
using AngleSharp.Html.Parser;
using SmartReader;

namespace AlfaNews.Core.Ingest;

public sealed record ArticleContent(string Title, string Text, DateTimeOffset? PublishedAt);

public static class ArticleExtractor
{
    public const int MinTextLength = 200;
    private const int MinAdapterTextLength = 60;

    private static readonly Dictionary<string, string> BodySelectors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1prime.ru"] = ".article__body .article__text",
        ["www.1prime.ru"] = ".article__body .article__text",
        ["government.ru"] = ".reader_article_body[itemprop=articleBody] p:not([class])",
        ["www.government.ru"] = ".reader_article_body[itemprop=articleBody] p:not([class])",
        ["minfin.gov.ru"] = ".presscenter_one .text_wrapper p",
        ["www.minfin.gov.ru"] = ".presscenter_one .text_wrapper p"
    };

    public static ArticleContent Extract(string html, Uri url)
    {
        var document = new HtmlParser().ParseDocument(html);
        var title = TextNormalizer.Normalize(document.QuerySelector("h1")?.TextContent
            ?? document.QuerySelector("meta[property='og:title']")?.GetAttribute("content") ?? document.Title ?? string.Empty);
        var published = ReadTime(document.QuerySelector("meta[property='article:published_time']")?.GetAttribute("content"))
            ?? ReadTime(document.QuerySelector("time[datetime]")?.GetAttribute("datetime"));

        if (BodySelectors.TryGetValue(url.Host, out var selector))
        {
            var blocks = document.QuerySelectorAll(selector).Select(node => TextNormalizer.Normalize(node.TextContent))
                .Where(text => text.Length > 0).ToList();
            return Build(title, string.Join("\n", blocks), published, MinAdapterTextLength);
        }

        var article = new Reader(url.ToString(), html).GetArticle();
        if (!article.Completed)
            throw new FetchException("empty_body", false, "Не удалось выделить текст статьи.");
        var readerPublished = article.PublicationDate is { } date && date.Year > 2000
            ? new DateTimeOffset(DateTime.SpecifyKind(date, date.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : date.Kind))
            : (DateTimeOffset?)null;
        return Build(string.IsNullOrWhiteSpace(article.Title) ? title : TextNormalizer.Normalize(article.Title),
            article.TextContent ?? string.Empty, published ?? readerPublished);
    }

    public static bool IsBoilerplateOnly(string text) =>
        text.TrimStart().StartsWith("Copyright", StringComparison.OrdinalIgnoreCase)
        && text.Contains("Все права защищены", StringComparison.OrdinalIgnoreCase);

    private static ArticleContent Build(string title, string text, DateTimeOffset? published, int minLength = MinTextLength)
    {
        var normalized = text.Contains('\n') ? string.Join("\n", text.Split('\n').Select(TextNormalizer.Normalize)) : TextNormalizer.Normalize(text);
        if (IsBoilerplateOnly(normalized))
            throw new FetchException("boilerplate_body", false, "Вместо текста статьи получен футер сайта.");
        if (TextNormalizer.Normalize(normalized).Length < minLength)
            throw new FetchException("empty_body", false, "Не удалось выделить текст статьи.");
        return new ArticleContent(title, normalized, published);
    }

    private static DateTimeOffset? ReadTime(string? value) =>
        !string.IsNullOrWhiteSpace(value) && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;
}