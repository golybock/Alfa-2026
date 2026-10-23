using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AlfaNews.Contracts.Domain;

namespace AlfaNews.Core.Insights;

public static partial class InsightChecks
{
    public const string Version = "insight-checks-v3";

    private static readonly string[] PaymentOverclaims = ["поставщик", "покупател", "доля поставок", "доли поставок", "доля закупок", "доли закупок",
        "доля выручки", "доли выручки", "доля затрат", "доли затрат"];
    private static readonly string[] SupplyOverclaims = ["поставк", "поставщик"];
    private static readonly string[] Negations = ["не подтвержда", "не доказыва", "не означа", "не являются", "не является", "не указыва", "нет данных",
        "данных нет", "отсутству", "не известн", "неизвестн", "не позволя"];

    public static IReadOnlyList<string> Validate(JsonElement draft, IReadOnlyDictionary<string, EvidenceDto> evidence,
        IReadOnlySet<string>? applicableBankEvidence = null)
    {
        var errors = ValidateClaims(draft, evidence).ToList();
        var claims = draft.GetProperty("claims").EnumerateArray().ToArray();
        var origins = claims.ToDictionary(claim => claim.GetProperty("id").GetString()!, claim => claim.GetProperty("origin").GetString()!,
            StringComparer.Ordinal);
        var bankView = draft.GetProperty("bank_view");
        if (bankView.GetProperty("status").GetString() == "found")
        {
            var ids = bankView.GetProperty("claim_ids").EnumerateArray().Select(item => item.GetString()!).ToArray();
            if (ids.Length == 0 || ids.Any(item => origins.GetValueOrDefault(item) != "bank_analytics"))
                errors.Add("bank_view:found_requires_bank_analytics_claims");
            else if (applicableBankEvidence is not null && !claims.Where(claim => ids.Contains(claim.GetProperty("id").GetString()))
                         .SelectMany(claim => claim.GetProperty("refs").EnumerateArray().Select(item => item.GetString()!))
                         .Any(applicableBankEvidence.Contains))
                errors.Add("bank_view:not_applicable");
        }
        if (string.IsNullOrWhiteSpace(draft.GetProperty("headline").GetString()))
            errors.Add("card:empty_headline");
        if (draft.GetProperty("what_happened").GetArrayLength() == 0)
            errors.Add("card:what_happened_required");
        if (draft.GetProperty("suggested_questions").GetArrayLength() > 3)
            errors.Add("card:too_many_questions");
        return errors.Distinct(StringComparer.Ordinal).Take(12).ToArray();
    }

    // Общие проверки утверждений карточки и ответа чата.
    public static IReadOnlyList<string> ValidateClaims(JsonElement output, IReadOnlyDictionary<string, EvidenceDto> evidence)
    {
        var errors = new List<string>();
        foreach (var claim in output.GetProperty("claims").EnumerateArray())
        {
            var id = claim.GetProperty("id").GetString()!;
            var origin = claim.GetProperty("origin").GetString();
            var text = claim.GetProperty("text").GetString() ?? "";
            var refs = claim.GetProperty("refs").EnumerateArray().Select(item => item.GetString()!)
                .Where(evidence.ContainsKey).Select(item => evidence[item]).ToArray();
            if (origin == "bank_analytics" && refs.All(item => item.Type != EvidenceType.BankResearch))
                errors.Add($"claims:{id}:bank_analytics_requires_bank_research");
            if (origin == "ai_hypothesis" && claim.GetProperty("confidence").ValueKind == JsonValueKind.Null)
                errors.Add($"claims:{id}:hypothesis_requires_confidence");
            if (origin is "source_fact" or "bank_analytics")
            {
                var supported = refs.SelectMany(EvidenceNumbers).ToHashSet(StringComparer.Ordinal);
                if (Numbers(text).Any(number => !supported.Contains(number)))
                    errors.Add($"claims:{id}:number_not_in_evidence");
            }
            var asserted = origin is "source_fact" or "ai_inference" && !Negations.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
            if (asserted && refs.Any(item => item.Type == EvidenceType.PaymentAggregate)
                && PaymentOverclaims.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase)))
                errors.Add($"claims:{id}:payment_overclaim");
            if (asserted && refs.Any(item => item is { Type: EvidenceType.CompanyFeature, Key: "fts_countries" })
                && SupplyOverclaims.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase)))
                errors.Add($"claims:{id}:fts_overclaim");
        }
        return errors;
    }

    public static IEnumerable<string> Numbers(string text) => NumberPattern().Matches(text)
        .Select(match => Normalize(match.Value));

    private static IEnumerable<string> EvidenceNumbers(EvidenceDto evidence)
    {
        var raw = string.Join(" ", new[] { evidence.Quote, evidence.Value?.GetRawText(), evidence.Source,
            evidence.Date?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), evidence.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            evidence.Page?.ToString(CultureInfo.InvariantCulture) }.Where(item => item is not null));
        foreach (var number in Numbers(raw))
        {
            yield return number;
            if (decimal.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value is > 0 and < 1)
            {
                yield return Normalize((value * 100).ToString("0", CultureInfo.InvariantCulture));
                yield return Normalize(Math.Round(value * 100, 1).ToString("0.0", CultureInfo.InvariantCulture));
                yield return Normalize(((1 - value) * 100).ToString("0", CultureInfo.InvariantCulture));
                yield return Normalize(Math.Round((1 - value) * 100, 1).ToString("0.0", CultureInfo.InvariantCulture));
            }
        }
        if (evidence.Date is { } date)
        {
            yield return date.Day.ToString(CultureInfo.InvariantCulture);
            yield return date.Year.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static string Normalize(string number)
    {
        var value = number.Replace(',', '.').Replace(" ", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        return value.Contains('.') ? value.TrimEnd('0').TrimEnd('.') : value.TrimStart('0') is { Length: > 0 } trimmed ? trimmed : "0";
    }

    [GeneratedRegex(@"\d+(?:[,.]\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();
}