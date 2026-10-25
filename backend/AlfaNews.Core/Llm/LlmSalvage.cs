using System.Text.Json;
using System.Text.Json.Nodes;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Ingest;
using AlfaNews.Core.Insights;

namespace AlfaNews.Core.Llm;

public sealed record LlmSalvageResult(string Content, IReadOnlyList<string> Dropped);

// Факты новости и утверждения карточки независимы: ошибочный элемент убирается кодом вместо починки всего ответа.
// Починка остаётся для ошибок всего ответа: схема, заголовок, пустые обязательные блоки, теги.
public static class LlmSalvage
{
    public const string Version = "salvage-v4";
    private static readonly string[] Sections = ["what_happened", "why_it_matters", "actions"];

    private static readonly Dictionary<string, string> ClaimReasons = new(StringComparer.Ordinal)
    {
        ["number_not_in_evidence"] = "число не найдено в данных",
        ["unknown_evidence"] = "ссылка на несуществующие данные",
        ["unreviewed_chart_number"] = "число с непроверенного графика",
        ["payment_overclaim"] = "вывод сильнее платёжных данных",
        ["fts_overclaim"] = "вывод сильнее данных ФТС",
        ["hypothesis_requires_confidence"] = "гипотеза без уверенности",
        ["bank_analytics_requires_bank_research"] = "аналитика банка без обзора банка",
        ["bank_research_only_in_bank_view"] = "аналитика банка вне блока банка",
        ["dated_bank_view_without_date"] = "старый обзор банка без даты",
        ["too_long"] = "слишком длинное",
        ["unknown_claim_id"] = "ссылка на несуществующее утверждение"
    };

    public static LlmSalvageResult? TryApply(string content, LlmRequest request, IReadOnlyList<string> errors,
        Func<string, IReadOnlyList<string>> validate) => request.Task switch
    {
        LlmTask.NewsExtract => News(content, request),
        LlmTask.Draft => Draft(content, errors, validate),
        LlmTask.Chat => Chat(content, errors, validate),
        _ => null
    };

    public static string Describe(IReadOnlyList<string> dropped) => string.Join(", ", dropped
        .Select(item => item.Split(':')).Where(parts => parts is ["claim", _, _])
        .GroupBy(parts => ClaimReasons.GetValueOrDefault(parts[2], parts[2]))
        .Select(group => $"{group.Key} — {string.Join(", ", group.Select(parts => parts[1]))}"));

    private static LlmSalvageResult? News(string content, LlmRequest request)
    {
        var root = JsonNode.Parse(content)!.AsObject();
        var facts = root["facts"]!.AsArray();
        var lines = NewsLines.Read(request.Values["INPUT_JSON"]);
        var dropped = new List<string>();
        for (var index = facts.Count - 1; index >= 0; index--)
        {
            var fact = facts[index].Deserialize<FactReference>(LlmOutputValidator.JsonOptions)!;
            var error = NewsLines.Resolve(lines, fact).Error;
            if (error == "news:invalid_range")
            {
                // Границы цитаты неверны: цитатой становится вся строка, если числа факта в ней есть.
                facts[index]!["start"] = null;
                facts[index]!["end"] = null;
                error = NewsLines.Resolve(lines, fact with { Start = null, End = null }).Error;
                dropped.Add($"fact:{fact.Line}:{(error is null ? "range_reset" : error["news:".Length..])}");
            }
            else if (error is not null)
                dropped.Add($"fact:{fact.Line}:{error["news:".Length..]}");

            if (error is not null)
                facts.RemoveAt(index);
        }

        if (dropped.Count == 0 || facts.Count == 0)
            return null;
        dropped.Reverse();
        return new LlmSalvageResult(root.ToJsonString(), dropped);
    }

    private static LlmSalvageResult? Draft(string content, IReadOnlyList<string> errors,
        Func<string, IReadOnlyList<string>> validate)
    {
        var root = JsonNode.Parse(content)!.AsObject();
        var claims = root["claims"]!.AsArray();
        var hadActions = root["actions"]!.AsArray().Count > 0;
        var dropped = new List<string>();
        var droppedTexts = new List<string>();
        // Раздел ссылается на утверждение, которого нет в списке: висячая ссылка убирается, сами утверждения не трогаются.
        if (errors.Contains("claims:unknown_claim_id"))
        {
            var known = claims.Select(claim => claim!["id"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
            var dangling = References(root).Where(id => !known.Contains(id)).ToHashSet(StringComparer.Ordinal);
            if (dangling.Count > 0)
            {
                RemoveReferences(root, dangling);
                dropped.AddRange(dangling.Order(StringComparer.Ordinal).Select(id => $"claim:{id}:unknown_claim_id"));
                errors = validate(root.ToJsonString());
            }
        }

        // Убранное утверждение может открыть следующее (например, ссылку на него); три прохода хватает с запасом.
        for (var pass = 0; pass < 3; pass++)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parts in errors.Select(error => error.Split(':')))
                if (parts is ["claims", var id, var code] && ClaimReasons.ContainsKey(code) && ids.Add(id))
                    dropped.Add($"claim:{id}:{code}");
            if (ids.Count == 0)
                break;
            for (var index = claims.Count - 1; index >= 0; index--)
                if (ids.Contains(claims[index]!["id"]!.GetValue<string>()))
                {
                    droppedTexts.Add(claims[index]!["text"]!.GetValue<string>());
                    claims.RemoveAt(index);
                }

            RemoveReferences(root, ids);
            errors = validate(root.ToJsonString());
        }

        if (dropped.Count == 0)
            return null;
        // Заголовок называет проверку, которая раскрывается первым действием: без единого действия карточка ему противоречит.
        if (hadActions && root["actions"]!.AsArray().Count == 0)
            return null;
        // Заголовок не должен держаться на убранном утверждении: его число, оставшееся без опоры, — повод для починки.
        var kept = claims.SelectMany(claim => InsightChecks.Numbers(claim!["text"]!.GetValue<string>()))
            .ToHashSet(StringComparer.Ordinal);
        var removed = droppedTexts.SelectMany(InsightChecks.Numbers).ToHashSet(StringComparer.Ordinal);
        if (InsightChecks.Numbers(root["headline"]!.GetValue<string>())
            .Any(number => removed.Contains(number) && !kept.Contains(number)))
            return null;
        return new LlmSalvageResult(root.ToJsonString(), dropped);
    }

    // В ответе чата утверждения — опора связного текста, разделов нет: ошибочное утверждение просто убирается.
    // Если текст держался на его числе, проверка ответа не пройдёт и останется починка.
    private static LlmSalvageResult? Chat(string content, IReadOnlyList<string> errors,
        Func<string, IReadOnlyList<string>> validate)
    {
        var root = JsonNode.Parse(content)!.AsObject();
        var claims = root["claims"]!.AsArray();
        var dropped = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parts in errors.Select(error => error.Split(':')))
            if (parts is ["claims", var id, var code] && ClaimReasons.ContainsKey(code) && ids.Add(id))
                dropped.Add($"claim:{id}:{code}");
        if (ids.Count == 0)
            return null;
        for (var index = claims.Count - 1; index >= 0; index--)
            if (ids.Contains(claims[index]!["id"]!.GetValue<string>()))
                claims.RemoveAt(index);
        var cleaned = root.ToJsonString();
        return validate(cleaned).Count == 0 ? new LlmSalvageResult(cleaned, dropped) : null;
    }

    private static void RemoveReferences(JsonObject root, HashSet<string> ids)
    {
        foreach (var name in Sections)
            Filter(root[name]!.AsArray(), ids);
        var consequences = root["consequences"]!.AsArray();
        for (var index = consequences.Count - 1; index >= 0; index--)
        {
            var claimIds = consequences[index]!["claim_ids"]!.AsArray();
            Filter(claimIds, ids);
            if (claimIds.Count == 0)
                consequences.RemoveAt(index);
        }

        var bankView = root["bank_view"]!.AsObject();
        var bankIds = bankView["claim_ids"]!.AsArray();
        Filter(bankIds, ids);
        if (bankIds.Count == 0)
            bankView["status"] = "not_found";
    }

    private static IEnumerable<string> References(JsonObject root) =>
        Sections.SelectMany(name => root[name]!.AsArray())
            .Concat(root["consequences"]!.AsArray().SelectMany(item => item!["claim_ids"]!.AsArray()))
            .Concat(root["bank_view"]!["claim_ids"]!.AsArray())
            .Select(item => item!.GetValue<string>());

    private static void Filter(JsonArray array, HashSet<string> ids)
    {
        for (var index = array.Count - 1; index >= 0; index--)
            if (ids.Contains(array[index]!.GetValue<string>()))
                array.RemoveAt(index);
    }
}