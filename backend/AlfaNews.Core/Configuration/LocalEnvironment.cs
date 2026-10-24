using System.Text.RegularExpressions;
using Npgsql;

namespace AlfaNews.Core.Configuration;

public static partial class LocalEnvironment
{
    public static void Load()
    {
        var inherited = Aliases.SelectMany(pair => new[] { pair.Key, pair.Value }).Distinct()
            .ToDictionary(name => name, Environment.GetEnvironmentVariable);
        var path = FindFile(Directory.GetCurrentDirectory()) ?? FindFile(AppContext.BaseDirectory);
        if (path is not null)
            foreach (var (name, value) in Parse(File.ReadAllText(path)))
                SetDefault(name, value);

        foreach (var (source, target) in Aliases)
            if (inherited[target] is null && inherited[source] is { } inheritedValue)
                Environment.SetEnvironmentVariable(target, inheritedValue);
            else if (Environment.GetEnvironmentVariable(source) is { } value)
                SetDefault(target, value);

        if (Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") is null
            && Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") is { } password)
        {
            var connection = new NpgsqlConnectionStringBuilder
            {
                Host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "127.0.0.1",
                Port = int.Parse(Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "15432"),
                Database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "alfanews",
                Username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "alfanews",
                Password = password
            };
            SetDefault("ConnectionStrings__DefaultConnection", connection.ConnectionString);
        }
    }

    public static IReadOnlyDictionary<string, string> Parse(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in text.Split('\n'))
        {
            var line = source.Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            if (line.StartsWith("export ", StringComparison.Ordinal))
                line = line[7..].TrimStart();
            var separator = line.IndexOf('=');
            if (separator <= 0 || !VariableName().IsMatch(line[..separator].Trim()))
                throw new FormatException("Некорректная строка .env.");
            var name = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (value.StartsWith('\'') || value.StartsWith('"'))
            {
                var quote = value[0];
                var end = value.LastIndexOf(quote);
                if (end == 0 || value[(end + 1)..].Trim() is { Length: > 0 } tail && !tail.StartsWith('#'))
                    throw new FormatException("Некорректные кавычки в .env.");
                value = value[1..end];
            }
            else
            {
                var comment = value.IndexOf(" #", StringComparison.Ordinal);
                if (comment >= 0)
                    value = value[..comment].TrimEnd();
            }
            values[name] = value;
        }
        return values;
    }

    private static string? FindFile(string start)
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, ".env");
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    private static void SetDefault(string name, string value)
    {
        if (Environment.GetEnvironmentVariable(name) is null)
            Environment.SetEnvironmentVariable(name, value);
    }

    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>
    {
        ["LLM_USE_MOCK"] = "Llm__UseMock",
        ["LLM_BASE_URL"] = "Llm__BaseUrl",
        ["LLM_API_KEY"] = "Llm__ApiKey",
        ["LLM_STRUCTURED_OUTPUT"] = "Llm__StructuredOutput",
        ["LLM_DEEPSEEK_THINKING_ENABLED"] = "Llm__DeepSeekThinkingEnabled",
        ["LLM_EXTRACT_MODEL"] = "Llm__ExtractModel",
        ["LLM_EXTRACT_THINKING_ENABLED"] = "Llm__ExtractThinkingEnabled",
        ["LLM_GLM_REASONING_LEVEL"] = "Llm__GlmReasoningLevel",
        ["DEEPSEEK_MODEL_ID"] = "Llm__DeepSeekModel",
        ["GLM_MODEL_ID"] = "Llm__GlmModel",
        ["LLM_DEEPSEEK_INPUT_ESTIMATE_RUB_PER_MILLION"] = "Llm__DeepSeekInputEstimateRubPerMillion",
        ["LLM_DEEPSEEK_OUTPUT_ESTIMATE_RUB_PER_MILLION"] = "Llm__DeepSeekOutputEstimateRubPerMillion",
        ["LLM_GLM_INPUT_ESTIMATE_RUB_PER_MILLION"] = "Llm__GlmInputEstimateRubPerMillion",
        ["LLM_GLM_OUTPUT_ESTIMATE_RUB_PER_MILLION"] = "Llm__GlmOutputEstimateRubPerMillion",
        ["CRITIC_ENABLED"] = "Llm__CriticEnabled",
        ["CRITIC_SCOPE"] = "Llm__CriticScope",
        ["FEED_COMPANY_IDS"] = "Feed__CompanyIds",
        ["FEED_MODE"] = "Feed__Mode",
        ["QUEUE_NEWS_SLOTS"] = "Queue__NewsSlots",
        ["QUEUE_DOCUMENTS_SLOTS"] = "Queue__DocumentsSlots",
        ["QUEUE_INSIGHTS_SLOTS"] = "Queue__InsightsSlots",
        ["QUEUE_CHAT_SLOTS"] = "Queue__ChatSlots",
        ["LLM_MAX_PARALLEL_REQUESTS"] = "Llm__MaxParallelRequests",
        ["LLM_DAILY_LIMIT_RUB"] = "Llm__DailyLimitRub"
    };

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex VariableName();
}