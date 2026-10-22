using System.Text.Json;
using System.Text.Json.Nodes;
using AlfaNews.Contracts;
using Json.Schema;

namespace AlfaNews.Core.Llm;

public static class LlmOutputValidator
{
    public static JsonSerializerOptions JsonOptions { get; } = CreateOptions();

    public static IReadOnlyList<string> Validate<T>(string content, LlmPrompt prompt, LlmRequest request, out T? value)
    {
        value = default;
        try
        {
            var json = JsonNode.Parse(content);
            var result = JsonSchema.FromText(prompt.Schema).Evaluate(json,
                new EvaluationOptions { OutputFormat = OutputFormat.List, RequireFormatValidation = true });
            if (!result.IsValid)
                return result.Details?.Where(detail => detail.Errors is { Count: > 0 }).Take(12)
                    .Select(detail => "schema:" + detail.InstanceLocation).ToArray() is { Length: > 0 } errors
                    ? errors : ["schema:invalid"];
            value = JsonSerializer.Deserialize<T>(content, JsonOptions);
            if (value is null)
                return ["schema:null"];
            var element = JsonSerializer.Deserialize<JsonElement>(content);
            return LlmSemanticChecks.Validate(element, request)
                .Concat(request.ValidateOutput?.Invoke(element) ?? []).Take(12).ToArray();
        }
        catch (JsonException)
        {
            return ["schema:invalid_json"];
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        ContractJson.Configure(options);
        return options;
    }
}