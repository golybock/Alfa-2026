using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AlfaNews.Contracts.Processing;

namespace AlfaNews.Core.Llm;

public sealed partial class LlmPromptCatalog
{
    private readonly string root;
    private readonly JsonElement manifest;
    public string VersionHash => Hash(manifest.GetRawText());

    public LlmPromptCatalog() : this(Path.Combine(AppContext.BaseDirectory, "Prompts")) { }

    public LlmPromptCatalog(string directory)
    {
        root = directory;
        manifest = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(root, "versions.json")));
        foreach (var entry in manifest.GetProperty("sha256").EnumerateObject())
        {
            var text = File.ReadAllText(Path.Combine(root, entry.Name)).Replace("\r\n", "\n").TrimEnd();
            if (Hash(text) != entry.Value.GetString())
                throw new InvalidOperationException("Контрольная сумма промпта не совпадает: " + entry.Name);
        }
    }

    public LlmPrompt Render(LlmRequest request, LlmTask task, string? previous = null,
        IReadOnlyList<string>? diagnostics = null)
    {
        var original = manifest.GetProperty("tasks").GetProperty(request.Task.ToString());
        var target = manifest.GetProperty("tasks").GetProperty(task.ToString());
        var schemaName = Path.GetFileName(original.GetProperty("schema").GetString()!);
        var schema = File.ReadAllText(Path.Combine(root, "schemas", schemaName));
        var replacements = request.Values.ToDictionary(pair => pair.Key, pair => pair.Value.GetRawText(), StringComparer.Ordinal);
        replacements["OUTPUT_SCHEMA_JSON"] = schema;
        if (task == LlmTask.Repair)
        {
            var originalPrompt = Render(request, request.Task);
            replacements["ORIGINAL_SYSTEM_RULES"] = originalPrompt.System.Replace(originalPrompt.Schema,
                "Use the target output schema supplied below.", StringComparison.Ordinal);
            replacements["ORIGINAL_INPUT_JSON"] = JsonSerializer.Serialize(request.Values);
            replacements["PREVIOUS_RESPONSE_JSON"] = JsonSerializer.Serialize(previous);
            replacements["DIAGNOSTICS_JSON"] = JsonSerializer.Serialize((diagnostics ?? []).Take(12).Select(error => error[..Math.Min(error.Length, 240)]));
        }
        var system = Replace(File.ReadAllText(Path.Combine(root, target.GetProperty("system").GetString()!)), replacements);
        var user = Replace(File.ReadAllText(Path.Combine(root, target.GetProperty("user").GetString()!)), replacements);
        var runtime = request.Runtime ?? new LlmRuntime();
        system += "\nServer runtime settings:\n" + JsonSerializer.Serialize(new
        {
            permitted_tools = runtime.Tools ?? new Dictionary<string, JsonElement>(),
            remaining_tool_calls = runtime.RemainingToolCalls,
            forecast_enabled = runtime.ForecastEnabled && runtime.Rank == 1,
            rank = runtime.Rank
        });
        return new LlmPrompt(system, user, schema, schemaName[..^12],
            target.GetProperty("prompt_version").GetString()!, Hash(schema));
    }

    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Replace(string template, IReadOnlyDictionary<string, string> replacements) =>
        Placeholder().Replace(template, match => replacements.TryGetValue(match.Groups[1].Value, out var value)
            ? value : throw new ArgumentException("Не задан вход промпта: " + match.Groups[1].Value));

    [GeneratedRegex("\\{\\{([A-Z_]+)\\}\\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}

public sealed record LlmPrompt(string System, string User, string Schema, string SchemaName, string Version, string SchemaHash);