using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace AlfaNews.Core.Ingest;

public sealed record FeedEntry(string Url, string Title, DateTimeOffset? PublishedAt, string? FullText);

public static class RssReader
{
    private static readonly string[] FullTextNames = ["full-text", "encoded"];

    public static IReadOnlyList<FeedEntry> Parse(string xml)
    {
        IEnumerable<XElement> items;
        try
        {
            items = Load(xml).Descendants().Where(element => element.Name.LocalName is "item" or "entry").ToList();
        }
        catch (XmlException)
        {
            var salvaged = SalvageItems(xml);
            if (salvaged.Count == 0)
                throw;
            items = salvaged;
        }
        var entries = new List<FeedEntry>();
        foreach (var item in items)
        {
            var url = ReadLink(item);
            var title = Clean(Child(item, "title"));
            if (url is null || string.IsNullOrWhiteSpace(title))
                continue;
            entries.Add(new FeedEntry(url, title, ReadDate(item), ReadFullText(item)));
        }
        return entries;
    }

    private static XDocument Load(string xml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(new StringReader(xml), settings);
        return XDocument.Load(reader);
    }

    private static List<XElement> SalvageItems(string xml)
    {
        var items = new List<XElement>();
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(xml, @"<item[ >].*?</item>",
                     System.Text.RegularExpressions.RegexOptions.Singleline))
        {
            try
            {
                items.Add(Load(match.Value).Root!);
            }
            catch (XmlException)
            {
            }
        }
        return items;
    }

    private static string? ReadLink(XElement item)
    {
        var links = item.Elements().Where(element => element.Name.LocalName == "link").ToList();
        var atomLink = links.FirstOrDefault(element => element.Attribute("href") is not null
            && (element.Attribute("rel")?.Value ?? "alternate") == "alternate");
        var value = atomLink?.Attribute("href")?.Value ?? links.Select(element => element.Value).FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DateTimeOffset? ReadDate(XElement item)
    {
        foreach (var name in new[] { "pubDate", "published", "updated", "date" })
        {
            var text = Child(item, name);
            if (text is not null && DateTimeOffset.TryParse(text.Trim(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var date))
                return date;
        }
        return null;
    }

    private static string? ReadFullText(XElement item)
    {
        foreach (var name in FullTextNames)
        {
            var element = item.Elements().FirstOrDefault(candidate => candidate.Name.LocalName == name);
            if (element is not null && !string.IsNullOrWhiteSpace(element.Value))
                return name == "encoded" ? TextNormalizer.FromHtml(element.Value) : TextNormalizer.Normalize(element.Value);
        }
        return null;
    }

    private static string? Child(XElement item, string localName) =>
        item.Elements().FirstOrDefault(element => element.Name.LocalName == localName)?.Value;

    private static string Clean(string? value) => TextNormalizer.Normalize(System.Net.WebUtility.HtmlDecode(value ?? string.Empty));
}