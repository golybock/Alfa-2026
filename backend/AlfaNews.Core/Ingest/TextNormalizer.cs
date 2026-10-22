using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;

namespace AlfaNews.Core.Ingest;

public static partial class TextNormalizer
{
    private static readonly string[] TrackingKeys = ["yclid", "gclid", "fbclid", "_openstat"];

    public static string Normalize(string text) => WhitespaceRegex().Replace(text.Replace(' ', ' '), " ").Trim();

    public static string FromHtml(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        return Normalize(document.Body?.TextContent ?? string.Empty);
    }

    public static string Sha256(string normalizedText) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedText)));

    public static string CanonicalUrl(Uri url)
    {
        var kept = url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(pair => !IsTracking(pair.Split('=')[0]))
            .ToList();
        var builder = new UriBuilder(url) { Fragment = string.Empty, Query = string.Join('&', kept) };
        return builder.Uri.GetLeftPart(UriPartial.Path) + (kept.Count > 0 ? "?" + string.Join('&', kept) : string.Empty);
    }

    private static bool IsTracking(string key) =>
        key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase)
        || TrackingKeys.Contains(key, StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}