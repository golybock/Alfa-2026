using AlfaNews.Core.Ingest;

namespace AlfaNews.Tests;

public sealed class IngestParsingTests
{
    [Fact]
    public void DetectsCopyrightFooterWithoutRejectingArticleWithCopyrightAtTheEnd()
    {
        Assert.True(ArticleExtractor.IsBoilerplateOnly("Copyright © 1991—2026 Interfax. Все права защищены. Сайт использует cookie."));
        Assert.False(ArticleExtractor.IsBoilerplateOnly("Компания сообщила о росте выручки. Copyright © 2026. Все права защищены."));
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ingest", name));

    [Fact]
    public void RssReaderReadsDatesLinksAndFullTextFromRealFeedShapes()
    {
        var interfax = RssReader.Parse(Fixture("feed-interfax.xml"));
        Assert.Equal(3, interfax.Count);
        Assert.All(interfax, entry => Assert.NotNull(entry.PublishedAt));
        Assert.Equal(TimeSpan.FromHours(3), interfax[0].PublishedAt!.Value.Offset);
        Assert.StartsWith("https://www.interfax.ru/", interfax[0].Url);
        Assert.All(interfax, entry => Assert.Null(entry.FullText));

        var rbc = RssReader.Parse(Fixture("feed-rbc.xml"));
        Assert.Equal(2, rbc.Count);
        Assert.All(rbc, entry => Assert.True(entry.FullText?.Length >= 400));
    }

    [Fact]
    public void RssReaderStripsEscapedHtmlFromYandexFullText()
    {
        const string feed = """
            <rss xmlns:yandex="http://news.yandex.ru" version="2.0"><channel><item><title>Банкротство</title>
            <link>https://rusbankrot.ru/a/</link><pubDate>Thu, 22 Oct 2026 18:39:11 +0300</pubDate>
            <yandex:full-text>Кратко о деле.&amp;lt;p&amp;gt;Первый абзац с &amp;quot;кавычками&amp;quot;.&amp;lt;/p&amp;gt; &amp;lt;h2&amp;gt;Заголовок&amp;lt;/h2&amp;gt;&amp;lt;p&amp;gt;Второй абзац.&amp;lt;/p&amp;gt;</yandex:full-text>
            </item></channel></rss>
            """;
        var entry = Assert.Single(RssReader.Parse(feed));
        Assert.Equal("Кратко о деле. Первый абзац с \"кавычками\". Заголовок Второй абзац.", entry.FullText);
    }

    [Fact]
    public void RssReaderSalvagesCompleteItemsFromTruncatedFeed()
    {
        var entries = RssReader.Parse(Fixture("feed-government-truncated.xml"));
        Assert.NotEmpty(entries);
        Assert.All(entries, entry => Assert.StartsWith("http://government.ru/news/", entry.Url));
    }

    [Fact]
    public void RssReaderReadsAtomEntries()
    {
        const string atom = """
            <feed xmlns="http://www.w3.org/2005/Atom"><entry><title>Заголовок</title>
            <link rel="alternate" href="https://example.org/a"/><updated>2026-10-22T10:00:00+03:00</updated></entry></feed>
            """;
        var entry = Assert.Single(RssReader.Parse(atom));
        Assert.Equal("https://example.org/a", entry.Url);
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.FromHours(3)), entry.PublishedAt);
    }

    [Fact]
    public void CanonicalUrlDropsTrackingAndFragmentButKeepsSignificantParameters()
    {
        Assert.Equal("https://minfin.gov.ru/ru/press-center/?id_4=40706",
            TextNormalizer.CanonicalUrl(new Uri("https://minfin.gov.ru/ru/press-center/?id_4=40706&utm_source=x#top")));
        Assert.Equal("https://www.interfax.ru/world/1124357", TextNormalizer.CanonicalUrl(new Uri("https://www.interfax.ru/world/1124357?utm_medium=rss")));
    }

    [Fact]
    public void TextHashIgnoresWhitespaceDifferences()
    {
        Assert.Equal(TextNormalizer.Sha256(TextNormalizer.Normalize("а  б\n в")), TextNormalizer.Sha256(TextNormalizer.Normalize("а б в")));
    }

    [Theory]
    [InlineData("article-interfax.html", "https://www.interfax.ru/business/1124356")]
    [InlineData("article-prime.html", "https://1prime.ru/20261022/rynok-874534258.html")]
    [InlineData("article-government.html", "http://government.ru/news/60269/")]
    public void ArticleExtractorReturnsBodyTextWithoutMenusOrWidgets(string file, string url)
    {
        var article = ArticleExtractor.Extract(Fixture(file), new Uri(url));
        Assert.True(article.Text.Length >= ArticleExtractor.MinTextLength);
        Assert.Contains("Синтетический абзац", article.Text);
        Assert.DoesNotContain("Подписка", article.Text);
        Assert.DoesNotContain("Все сервисы", article.Text);
    }

    [Fact]
    public void ArticleExtractorReadsPublicationDateFromPage()
    {
        var article = ArticleExtractor.Extract(Fixture("article-interfax.html"), new Uri("https://www.interfax.ru/business/1124356"));
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 19, 34, 0, TimeSpan.FromHours(3)), article.PublishedAt);
    }

    [Fact]
    public void ArticleExtractorRejectsPagesWithoutArticleText()
    {
        var exception = Assert.Throws<FetchException>(() =>
            ArticleExtractor.Extract("<html><body><nav>Меню</nav><p>Короткий текст</p></body></html>", new Uri("https://www.interfax.ru/x")));
        Assert.Equal("empty_body", exception.Code);
        Assert.False(exception.Transient);
    }
}