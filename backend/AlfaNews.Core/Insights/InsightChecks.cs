using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;

namespace AlfaNews.Core.Insights;

public static partial class InsightChecks
{
    public const string Version = "insight-checks-v5";
    public const int MaxHeadlineLength = 200;
    public const int MaxWhyYouLength = 200;

    // «Почему вы» — одна фраза модели рядом с заголовком. Её ошибки не стоят починки всей карточки: пустую или слишком
    // длинную код не показывает, фронт тогда остаётся без фразы.
    public static string? WhyYou(string? text) =>
        text?.Trim() is { Length: > 0 and <= MaxWhyYouLength } value ? value : null;
    public const int MaxClaimLength = 400;
    public const int MaxWhatHappened = 2;
    public const int MaxConsequences = 3;
    public const int MaxActions = 3;
    public const int MaxLimitations = 3;
    private static readonly string[] Months = ["января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря"];

    private static readonly string[] PaymentOverclaims = ["поставщик", "покупател", "доля поставок", "доли поставок", "доля закупок", "доли закупок",
        "доля выручки", "доли выручки", "доля затрат", "доли затрат"];
    private static readonly string[] SupplyOverclaims = ["поставк", "поставщик"];
    private static readonly string[] Negations = ["не подтвержда", "не доказыва", "не означа", "не являются", "не является", "не указыва", "нет данных",
        "данных нет", "отсутству", "не известн", "неизвестн", "не позволя"];

    public static IReadOnlyList<string> Validate(JsonElement draft, IReadOnlyDictionary<string, EvidenceDto> evidence,
        IReadOnlySet<string>? applicableBankEvidence = null, IReadOnlyDictionary<string, DateOnly>? datedBankEvidence = null)
    {
        var errors = ValidateClaims(draft, evidence).ToList();
        var claims = draft.GetProperty("claims").EnumerateArray().ToArray();
        var origins = claims.ToDictionary(claim => claim.GetProperty("id").GetString()!, claim => claim.GetProperty("origin").GetString()!,
            StringComparer.Ordinal);
        var bankView = draft.GetProperty("bank_view");
        // Аналитика банка показывается только в блоке «Как это оценивает банк»: в других блоках интерфейс подписывает её
        // как позицию банка, и при not_found карточка противоречит сама себе. Неприменимый обзор описывается в limitations.
        var bankIds = bankView.GetProperty("status").GetString() == "found"
            ? bankView.GetProperty("claim_ids").EnumerateArray().Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal)
            : [];
        var sectionIds = new[] { "what_happened", "why_it_matters", "actions" }
            .SelectMany(name => draft.TryGetProperty(name, out var section) ? section.EnumerateArray() : Enumerable.Empty<JsonElement>())
            .Concat(draft.TryGetProperty("consequences", out var consequences)
                ? consequences.EnumerateArray().SelectMany(item => item.GetProperty("claim_ids").EnumerateArray())
                : [])
            .Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal);
        foreach (var claim in claims)
        {
            var id = claim.GetProperty("id").GetString()!;
            if (origins[id] == "bank_analytics" ? !bankIds.Contains(id) || sectionIds.Contains(id)
                    : sectionIds.Contains(id) && claim.GetProperty("refs").EnumerateArray()
                        .Any(item => evidence.GetValueOrDefault(item.GetString()!)?.Type == EvidenceType.BankResearch))
                errors.Add($"claims:{id}:bank_research_only_in_bank_view");
            if (claim.GetProperty("text").GetString() is { Length: > MaxClaimLength })
                errors.Add($"claims:{id}:too_long");
            // Старый обзор — только оценка банка на свою дату: дата должна быть в тексте утверждения.
            var text = claim.GetProperty("text").GetString() ?? "";
            var datedRefs = datedBankEvidence is null ? [] : claim.GetProperty("refs").EnumerateArray()
                .Select(item => item.GetString()!).Where(datedBankEvidence.ContainsKey).Select(item => datedBankEvidence[item]).ToArray();
            if (datedRefs.Any(date => !MentionsDate(text, date)))
                errors.Add($"claims:{id}:dated_bank_view_without_date");
        }
        if (bankIds.Count > 0)
        {
            var ids = bankIds.ToArray();
            if (ids.Length == 0 || ids.Any(item => origins.GetValueOrDefault(item) != "bank_analytics"))
                errors.Add("bank_view:found_requires_bank_analytics_claims");
            else if (applicableBankEvidence is not null && !claims.Where(claim => ids.Contains(claim.GetProperty("id").GetString()))
                         .SelectMany(claim => claim.GetProperty("refs").EnumerateArray().Select(item => item.GetString()!))
                         .Any(applicableBankEvidence.Contains))
                errors.Add("bank_view:not_applicable");
        }
        if (string.IsNullOrWhiteSpace(draft.GetProperty("headline").GetString()))
            errors.Add("card:empty_headline");
        if (draft.GetProperty("headline").GetString() is { Length: > MaxHeadlineLength })
            errors.Add("card:headline_too_long");
        if (draft.GetProperty("what_happened").GetArrayLength() == 0)
            errors.Add("card:what_happened_required");
        if (draft.GetProperty("suggested_questions").GetArrayLength() > 3)
            errors.Add("card:too_many_questions");
        return errors.Distinct(StringComparer.Ordinal).Take(12).ToArray();
    }

    // Лишнее сверх лимитов экрана обрезается кодом, а не починкой: порядок в секциях задаёт модель (важное первым).
    // Утверждения, на которые после обрезки никто не ссылается, убираются из реестра.
    public static InsightDraft Trim(InsightDraft draft)
    {
        var trimmed = draft with
        {
            WhatHappened = draft.WhatHappened.Take(MaxWhatHappened).ToArray(),
            Consequences = draft.Consequences.Take(MaxConsequences).ToArray(),
            Actions = draft.Actions.Take(MaxActions).ToArray(),
            Limitations = draft.Limitations.Take(MaxLimitations).ToArray()
        };
        var used = trimmed.WhatHappened.Concat(trimmed.WhyItMatters).Concat(trimmed.Actions).Concat(trimmed.BankView.ClaimIds)
            .Concat(trimmed.Consequences.SelectMany(item => item.ClaimIds)).ToHashSet(StringComparer.Ordinal);
        return trimmed with { Claims = draft.Claims.Where(claim => used.Contains(claim.Id)).ToArray() };
    }

    private static bool MentionsDate(string text, DateOnly date) =>
        text.Contains(date.ToString("dd.MM", CultureInfo.InvariantCulture), StringComparison.Ordinal)
        || text.Contains($"{date.Day} {Months[date.Month - 1]}", StringComparison.OrdinalIgnoreCase);

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