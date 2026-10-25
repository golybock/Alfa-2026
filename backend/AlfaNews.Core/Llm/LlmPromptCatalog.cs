using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using AlfaNews.Contracts.Processing;

namespace AlfaNews.Core.Llm;

public sealed partial class LlmPromptCatalog
{
    // Вход модели пишется с кириллицей как есть: по умолчанию JSON экранирует её в \uXXXX, и русский текст стоит
    // в несколько раз больше токенов (замер 24.10: справочник разбора 26 тыс. символов вместо 7,7 тыс.).
    // Символы разметки (<, >, &) по-прежнему экранируются: недоверенный текст не закроет тег <input_json>.
    private static readonly JsonSerializerOptions PromptJson = new()
        { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    private const string SchemaByApi = "supplied by the API as the response_format JSON Schema; follow it exactly.";
    private readonly string root;
    private readonly JsonElement manifest;
    public string VersionHash => Hash(manifest.GetRawText());

    public LlmPromptCatalog() : this(Path.Combine(AppContext.BaseDirectory, "Prompts"))
    {
    }

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

    // schemaInPrompt = false, когда схема уходит в response_format: провайдер сам вставляет её в контекст модели
    // и берёт за неё входные токены (замер 24.10: схема карточки ~1 тыс. токенов), второй копии в тексте не нужно.
    public LlmPrompt Render(LlmRequest request, LlmTask task, string? previous = null,
        IReadOnlyList<string>? diagnostics = null, bool schemaInPrompt = true)
    {
        var original = manifest.GetProperty("tasks").GetProperty(request.Task.ToString());
        var target = manifest.GetProperty("tasks").GetProperty(task.ToString());
        var schemaName = Path.GetFileName(original.GetProperty("schema").GetString()!);
        var schema = File.ReadAllText(Path.Combine(root, "schemas", schemaName));
        var replacements = request.Values.ToDictionary(pair => pair.Key,
            pair => JsonSerializer.Serialize(pair.Value, PromptJson),
            StringComparer.Ordinal);
        replacements["OUTPUT_SCHEMA_JSON"] = schemaInPrompt ? schema : SchemaByApi;
        if (task == LlmTask.Repair)
        {
            var originalPrompt = Render(request, request.Task, schemaInPrompt: schemaInPrompt);
            replacements["ORIGINAL_SYSTEM_RULES"] = schemaInPrompt
                ? originalPrompt.System.Replace(originalPrompt.Schema, "Use the target output schema supplied below.",
                    StringComparison.Ordinal)
                : originalPrompt.System;
            replacements["ORIGINAL_INPUT_JSON"] = JsonSerializer.Serialize(request.Values, PromptJson);
            replacements["PREVIOUS_RESPONSE_JSON"] = PreviousResponse(previous);
            replacements["DIAGNOSTICS_JSON"] = JsonSerializer.Serialize((diagnostics ?? []).Take(12)
                .Select(error => error[..Math.Min(error.Length, 240)]), PromptJson);
        }

        var system = Replace(File.ReadAllText(Path.Combine(root, target.GetProperty("system").GetString()!)),
            replacements);
        var user = Replace(File.ReadAllText(Path.Combine(root, target.GetProperty("user").GetString()!)), replacements);
        var runtime = request.Runtime ?? new LlmRuntime();
        system += "\nServer runtime settings:\n" + JsonSerializer.Serialize(new
        {
            permitted_tools = runtime.Tools ?? new Dictionary<string, JsonElement>(),
            remaining_tool_calls = runtime.RemainingToolCalls,
            forecast_enabled = runtime.ForecastEnabled && runtime.Rank == 1,
            rank = runtime.Rank
        }, PromptJson);
        return new LlmPrompt(system, user, schema, schemaName[..^12],
            target.GetProperty("prompt_version").GetString()!, Hash(schema));
    }

    // Предыдущий ответ приходит уже готовым JSON (черновик агента сериализован с \uXXXX). Строкой его экранировало бы
    // второй раз, и модель видела бы \\u0410 вместо букв; поэтому валидный JSON вставляется как JSON, остальное — строкой.
    private static string PreviousResponse(string? previous)
    {
        if (previous is null)
            return "null";
        try
        {
            return JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(previous), PromptJson);
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(previous, PromptJson);
        }
    }

    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Replace(string template, IReadOnlyDictionary<string, string> replacements) =>
        Placeholder().Replace(template, match => replacements.TryGetValue(match.Groups[1].Value, out var value)
            ? value
            : throw new ArgumentException("Не задан вход промпта: " + match.Groups[1].Value));

    [GeneratedRegex("\\{\\{([A-Z_]+)\\}\\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}

public sealed record LlmPrompt(
    string System,
    string User,
    string Schema,
    string SchemaName,
    string Version,
    string SchemaHash);