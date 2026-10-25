using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Ingest;
using Json.Schema;

namespace AlfaNews.Core.Llm;

public static class LlmSemanticChecks
{
    public const string Version = "v5";

    public static IReadOnlyList<string> Validate(JsonElement value, LlmRequest request)
    {
        var errors = new List<string>();
        if (request.Task == LlmTask.NewsTriage)
        {
            var expected = request.Values["INPUT_JSON"].EnumerateArray()
                .Select(item => item.GetProperty("id").GetString()).ToHashSet();
            var actual = value.GetProperty("decisions").EnumerateArray()
                .Select(item => item.GetProperty("id").GetString()).ToArray();
            if (actual.Length != expected.Count || !expected.SetEquals(actual))
                errors.Add("triage:ids_must_match_exactly_once");
        }

        if (request.Task == LlmTask.NewsExtract)
        {
            var lines = NewsLines.Read(request.Values["INPUT_JSON"]);
            var facts = value.GetProperty("facts");
            if (lines.Count == 0 || facts.GetArrayLength() == 0)
                errors.Add("news:missing_facts");
            foreach (var fact in facts.Deserialize<FactReference[]>(LlmOutputValidator.JsonOptions)!)
                if (NewsLines.Resolve(lines, fact).Error is { } error)
                    errors.Add(error);
            Tags(value.GetProperty("tags"), request, errors);
        }

        if (request.Task == LlmTask.PdfOcr)
        {
            var blocks = value.GetProperty("blocks");
            if (blocks.GetArrayLength() == 0)
                errors.Add("pdf:empty_page_requires_review");
            var ids = new HashSet<string>();
            var chartIds = new HashSet<string>();
            foreach (var block in blocks.EnumerateArray())
            {
                if (!ids.Add(block.GetProperty("local_id").GetString()!))
                    errors.Add("pdf:duplicate_block_id");
                if (block.GetProperty("text").GetString()!.Contains("[неразборчиво]", StringComparison.Ordinal))
                    errors.Add("pdf:unreadable_content");
                Tags(block.GetProperty("tags"), request, errors);
                foreach (var table in block.GetProperty("tables").EnumerateArray())
                    if (table.GetProperty("rows").EnumerateArray().Any(row =>
                            row.GetArrayLength() != table.GetProperty("columns").GetArrayLength()))
                        errors.Add("pdf:ragged_table");
                foreach (var chart in block.GetProperty("charts").EnumerateArray())
                {
                    if (!chartIds.Add(chart.GetProperty("local_id").GetString()!))
                        errors.Add("pdf:duplicate_chart_id");
                    var region = chart.GetProperty("region").EnumerateArray().Select(item => item.GetDecimal())
                        .ToArray();
                    if (region.Length != 4 || region.Any(item => item is < 0 or > 1)
                                           || region[0] + region[2] > 1 || region[1] + region[3] > 1)
                        errors.Add("pdf:invalid_chart_region");
                    foreach (var point in chart.GetProperty("points").EnumerateArray())
                    {
                        var method = point.GetProperty("reading_method").GetString();
                        var approximate = point.GetProperty("approximate").GetBoolean();
                        if (method is not ("label" or "axis_estimate") || (method == "axis_estimate") != approximate
                                                                       || approximate &&
                                                                       !chart.GetProperty("description").GetString()!
                                                                           .Contains("по графику, приблизительно",
                                                                               StringComparison.OrdinalIgnoreCase))
                            errors.Add("pdf:invalid_chart_approximation");
                    }
                }
            }
        }

        if (request.Task is LlmTask.Draft or LlmTask.Chat)
        {
            var claims = value.GetProperty("claims").EnumerateArray().ToArray();
            var ids = claims.Select(claim => claim.GetProperty("id").GetString()!).ToHashSet(StringComparer.Ordinal);
            if (ids.Count != claims.Length)
                errors.Add("claims:duplicate_id");
            var context = request.Values["CONTEXT_JSON"];
            var evidence = Property(context, "evidence");
            foreach (var claim in claims)
            {
                var id = claim.GetProperty("id").GetString();
                var refs = claim.GetProperty("refs");
                if (refs.GetArrayLength() == 0 || refs.EnumerateArray().Any(reference =>
                        evidence.ValueKind != JsonValueKind.Object
                        || !evidence.TryGetProperty(reference.GetString()!, out _)))
                    errors.Add($"claims:{id}:unknown_evidence");
                foreach (var reference in refs.EnumerateArray())
                    if (evidence.ValueKind == JsonValueKind.Object && evidence.TryGetProperty(reference.GetString()!,
                                                                       out var source)
                                                                   && Property(source, "chart_id").ValueKind ==
                                                                   JsonValueKind.String
                                                                   && Property(source, "review_status").GetString() !=
                                                                   "reviewed"
                                                                   && Numbers(claim.GetProperty("text").GetString()!)
                                                                       .Count > 0)
                        errors.Add($"claims:{id}:unreviewed_chart_number");
            }

            if (request.Task == LlmTask.Draft)
            {
                foreach (var name in new[] { "what_happened", "why_it_matters", "actions" })
                    CheckIds(value.GetProperty(name), ids, errors);
                foreach (var consequence in value.GetProperty("consequences").EnumerateArray())
                    CheckIds(consequence.GetProperty("claim_ids"), ids, errors);
                var bankView = value.GetProperty("bank_view");
                CheckIds(bankView.GetProperty("claim_ids"), ids, errors);
                if (bankView.GetProperty("status").GetString() == "not_found" &&
                    bankView.GetProperty("claim_ids").GetArrayLength() != 0)
                    errors.Add("claims:bank_not_found_has_claims");
                if (value.TryGetProperty("forecast", out var forecast) && forecast.ValueKind != JsonValueKind.Null
                                                                       && request.Runtime is not
                                                                           { ForecastEnabled: true, Rank: 1 })
                    errors.Add("forecast:disabled");
            }
        }

        if (request.Task == LlmTask.Plan)
        {
            var calls = value.GetProperty("calls");
            var runtime = request.Runtime ?? new LlmRuntime();
            if (calls.GetArrayLength() > runtime.RemainingToolCalls)
                errors.Add("plan:tool_budget");
            foreach (var call in calls.EnumerateArray())
                if (runtime.Tools is null || !runtime.Tools.ContainsKey(call.GetProperty("name").GetString()!))
                    errors.Add("plan:unknown_tool");
                else if (!JsonSchema.FromText(runtime.Tools[call.GetProperty("name").GetString()!].GetRawText())
                             .Evaluate(JsonNode.Parse(call.GetProperty("arguments").GetRawText())).IsValid)
                    errors.Add("plan:invalid_arguments");
        }

        if (request.Task == LlmTask.Critic && request.Values.TryGetValue("DRAFT_JSON", out var draft))
        {
            var ids = draft.GetProperty("claims").EnumerateArray().Select(claim => claim.GetProperty("id").GetString())
                .ToHashSet();
            if (value.GetProperty("issues").EnumerateArray()
                .Any(issue => !ids.Contains(issue.GetProperty("claim_id").GetString())))
                errors.Add("critic:unknown_claim");
        }

        return errors;
    }

    private static void CheckIds(JsonElement references, HashSet<string> ids, List<string> errors)
    {
        if (references.EnumerateArray().Any(reference => !ids.Contains(reference.GetString()!)))
            errors.Add("claims:unknown_claim_id");
    }

    private static void Tags(JsonElement tags, LlmRequest request, List<string> errors)
    {
        var taxonomy = request.Values["TAXONOMY_JSON"];
        foreach (var group in tags.EnumerateObject())
        {
            var dictionary = Property(taxonomy, group.Name);
            HashSet<string?> allowed = dictionary.ValueKind == JsonValueKind.Array
                ? dictionary.EnumerateArray().Select(item =>
                        item.ValueKind == JsonValueKind.String ? item.GetString() : Property(item, "code").GetString())
                    .ToHashSet()
                : dictionary.ValueKind == JsonValueKind.Object
                    ? dictionary.EnumerateObject().Select(item => (string?)item.Name).ToHashSet()
                    : [];
            if (group.Value.EnumerateArray().Any(tag => !allowed.Contains(tag.GetString())))
                errors.Add("tags:unknown_" + group.Name);
        }
    }

    private static JsonElement Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property)
            ? property
            : JsonSerializer.SerializeToElement<string?>(null);

    private static HashSet<string> Numbers(string text) => Regex
        .Matches(text, @"\d+(?:[,.]\d+)?", RegexOptions.CultureInvariant)
        .Select(match => match.Value.Replace(',', '.')).ToHashSet(StringComparer.Ordinal);
}