using AlfaNews.Contracts.Processing;

namespace AlfaNews.Core.Llm;

public sealed class LlmOptions
{
    public bool UseMock { get; set; } = true;
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public const string DeepSeek = "deepseek/deepseek-v4.1-flash";
    public const string Glm = "z-ai/glm-5.3-flash";

    // Модели, разрешённые кейсом (приложение 3), в именах шлюза.
    public static readonly IReadOnlySet<string> AllowedModels = new HashSet<string>(StringComparer.Ordinal)
    {
        DeepSeek, Glm, "google/gemma-4-26b-a4b-it", "qwen/qwen3.6-35b-a3b", "qwen/qwen3-vl-30b-a3b-instruct", "moonshotai/kimi-k2.6"
    };

    // Модель на каждое использование, полным именем шлюза. Починка разбора и фильтра заголовков идёт на модели своей задачи,
    // остальные починки — на RepairModel. Распознаванию PDF нужна модель, принимающая изображения.
    public string TriageModel { get; set; } = DeepSeek;
    public string ExtractModel { get; set; } = DeepSeek;
    public string AgentModel { get; set; } = DeepSeek;
    public string RepairModel { get; set; } = DeepSeek;
    public string CriticModel { get; set; } = Glm;
    public string OcrModel { get; set; } = Glm;
    public string ChatModel { get; set; } = DeepSeek;
    public string ScopeGateModel { get; set; } = DeepSeek;
    // Рассуждения включены у всех задач, кроме фильтра заголовков; для разбора новостей — отдельный переключатель.
    // У GLM-5.3-Flash рассуждения принудительные, переключатели на неё не действуют.
    public bool ThinkingEnabled { get; set; } = true;
    public bool ExtractThinkingEnabled { get; set; } = true;
    public string ReasoningLevel { get; set; } = "medium";
    public string GlmReasoningLevel { get; set; } = "low";
    public bool StructuredOutput { get; set; } = true;
    public int MaxOutputTokens { get; set; } = 32000;
    public int ChatMaxOutputTokens { get; set; } = 8000;
    public int TimeoutSeconds { get; set; } = 240;
    public int CriticTimeoutSeconds { get; set; } = 120;
    public int MaxInputBytes { get; set; } = 200000;
    public int MaxParallelRequests { get; set; } = 8;
    public decimal DailyLimitRub { get; set; } = 500m;
    public decimal DeepSeekInputEstimateRubPerMillion { get; set; } = 55m;
    public decimal DeepSeekOutputEstimateRubPerMillion { get; set; } = 100m;
    public decimal GlmInputEstimateRubPerMillion { get; set; } = 22m;
    public decimal GlmOutputEstimateRubPerMillion { get; set; } = 50m;
    public int ImageInputTokenReserve { get; set; } = 32000;
    public int ReserveOutputTokens { get; set; } = 12000;
    public bool CriticEnabled { get; set; } = true;
    public string CriticScope { get; set; } = "all_full";
    public string ConfigVersion { get; set; } = "llm-v1";

    public string Model(LlmTask task, LlmTask originalTask) => task switch
    {
        LlmTask.Repair when originalTask is LlmTask.NewsTriage or LlmTask.NewsExtract => Model(originalTask, originalTask),
        LlmTask.NewsTriage => TriageModel,
        LlmTask.NewsExtract => ExtractModel,
        LlmTask.Plan or LlmTask.Draft => AgentModel,
        LlmTask.Repair => RepairModel,
        LlmTask.Critic => CriticModel,
        LlmTask.PdfOcr => OcrModel,
        LlmTask.Chat => ChatModel,
        LlmTask.ScopeGate => ScopeGateModel,
        _ => throw new ArgumentOutOfRangeException(nameof(task))
    };

    public IReadOnlyDictionary<string, string> Models() => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["triage"] = TriageModel,
        ["extract"] = ExtractModel,
        ["agent"] = AgentModel,
        ["repair"] = RepairModel,
        ["critic"] = CriticModel,
        ["ocr"] = OcrModel,
        ["chat"] = ChatModel,
        ["scope_gate"] = ScopeGateModel
    };

    public LlmRoute Route(LlmTask task, LlmTask originalTask)
    {
        var triage = originalTask == LlmTask.NewsTriage;
        var extract = originalTask == LlmTask.NewsExtract;
        var chat = originalTask is LlmTask.Chat or LlmTask.ScopeGate;
        var model = Model(task, originalTask);
        var glm = model == Glm;
        // Оценка цены для резерва бюджета: по семейству модели; для прочих разрешённых — по большей из известных.
        var (input, output) = model == DeepSeek ? (DeepSeekInputEstimateRubPerMillion, DeepSeekOutputEstimateRubPerMillion)
            : glm ? (GlmInputEstimateRubPerMillion, GlmOutputEstimateRubPerMillion)
            : (Math.Max(DeepSeekInputEstimateRubPerMillion, GlmInputEstimateRubPerMillion),
                Math.Max(DeepSeekOutputEstimateRubPerMillion, GlmOutputEstimateRubPerMillion));
        return new LlmRoute(model,
            glm ? GlmReasoningLevel : ReasoningLevel,
            triage ? 6000 : chat ? ChatMaxOutputTokens : MaxOutputTokens,
            task == LlmTask.Critic ? CriticTimeoutSeconds : TimeoutSeconds,
            input, output,
            glm || !triage && ThinkingEnabled && (!extract || ExtractThinkingEnabled));
    }

    public void Validate()
    {
        if (DailyLimitRub is <= 0 or > 500 || MaxOutputTokens is <= 0 or > 32000
            || ChatMaxOutputTokens is <= 0 or > 8000 || TimeoutSeconds <= 0 || CriticTimeoutSeconds <= 0
            || MaxInputBytes <= 0 || ImageInputTokenReserve <= 0 || ReserveOutputTokens <= 0 || MaxParallelRequests is < 1 or > 16
            || DeepSeekInputEstimateRubPerMillion < 0 || DeepSeekOutputEstimateRubPerMillion < 0
            || GlmInputEstimateRubPerMillion < 0 || GlmOutputEstimateRubPerMillion < 0
            || CriticScope is not ("all_full" or "top_only") || GlmReasoningLevel is not ("low" or "high")
            || ReasoningLevel is not ("low" or "medium" or "high") || string.IsNullOrWhiteSpace(ConfigVersion))
            throw new InvalidOperationException("Некорректные лимиты Llm.");
        var forbidden = Models().Where(pair => !AllowedModels.Contains(pair.Value)).Select(pair => $"{pair.Key}={pair.Value}").ToArray();
        if (forbidden.Length > 0)
            throw new InvalidOperationException(
                $"Модели не из разрешённого списка: {string.Join(", ", forbidden)}. Разрешены: {string.Join(", ", AllowedModels.Order(StringComparer.Ordinal))}.");
    }
}

public sealed record LlmRoute(string Model, string Reasoning, int MaxOutputTokens, int TimeoutSeconds,
    decimal InputRubPerMillion, decimal OutputRubPerMillion, bool ThinkingEnabled = true);