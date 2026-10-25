using System.Text.Json;
using System.Text.RegularExpressions;
using AlfaNews.Contracts.Processing;

namespace AlfaNews.Core.Ingest;

public sealed record LineQuote(string? Quote, string? Error);

public static partial class NewsLines
{
    public static IReadOnlyList<string> Split(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal)
        .Split('\n')
        .SelectMany(paragraph => SentenceBreak().Split(paragraph.Trim()))
        .Select(line => WhiteSpace().Replace(line, " ").Trim()).Where(line => line.Length > 0).ToArray();

    public static JsonElement Numbered(IReadOnlyList<string> lines) =>
        JsonSerializer.SerializeToElement(lines.Select((text, index) => new { n = index + 1, text }));

    public static IReadOnlyList<string> Read(JsonElement input)
    {
        if (input.ValueKind == JsonValueKind.Object && input.TryGetProperty("lines", out var lines) &&
            lines.ValueKind == JsonValueKind.Array)
            return lines.EnumerateArray().Select(line => line.GetProperty("text").GetString() ?? "").ToArray();
        var body = input.ValueKind != JsonValueKind.Object ? null
            : input.TryGetProperty("text", out var text) ? text.GetString()
            : input.TryGetProperty("body", out var raw) ? raw.GetString() : null;
        return Split(body ?? "");
    }

    public static LineQuote Resolve(IReadOnlyList<string> lines, FactReference fact)
    {
        if (fact.Line < 1 || fact.Line > lines.Count)
            return new LineQuote(null, "news:unknown_line");
        var line = lines[fact.Line - 1];
        if (fact.Start is null != fact.End is null ||
            fact.Start is { } from && (from < 0 || from >= fact.End || fact.End > line.Length))
            return new LineQuote(null, "news:invalid_range");
        var span = fact.Start is { } start ? line[start..fact.End!.Value].Trim() : line;
        var numbers = Numbers(fact.Text);
        if (span.Length >= 3 && numbers.IsSubsetOf(Numbers(span)))
            return new LineQuote(span, null);
        return numbers.IsSubsetOf(Numbers(line))
            ? new LineQuote(line, null)
            : new LineQuote(null, "news:number_not_in_quote");
    }

    public static HashSet<string> Numbers(string text) => NumberPattern().Matches(text)
        .Select(match => match.Value.Replace(',', '.')).ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex(@"(?<=[.!?…])\s+(?=[«""“„(\p{Lu}\d—–-])", RegexOptions.CultureInvariant)]
    private static partial Regex SentenceBreak();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhiteSpace();

    [GeneratedRegex(@"\d+(?:[,.]\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();
}