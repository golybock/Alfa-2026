using AlfaNews.Contracts.Processing;

namespace AlfaNews.Core.Llm;

public sealed class LlmOptions
{
    public bool UseMock { get; set; } = true;
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string DeepSeekModel { get; set; } = "deepseek/deepseek-v4.1-flash";
    public string GlmModel { get; set; } = "z-ai/glm-5.3-flash";
    public string DeepSeekReasoningLevel { get; set; } = "medium";
    public string GlmReasoningLevel { get; set; } = "low";
    public bool DeepSeekThinkingEnabled { get; set; } = true;
    public bool TriageUseGlm { get; set; }
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

    public LlmRoute Route(LlmTask task, LlmTask originalTask)
    {
        var triage = originalTask == LlmTask.NewsTriage;
        var glm = task is LlmTask.PdfOcr or LlmTask.Critic || triage && TriageUseGlm;
        var chat = originalTask is LlmTask.Chat or LlmTask.ScopeGate;
        return new LlmRoute(glm ? GlmModel : DeepSeekModel,
            glm ? GlmReasoningLevel : DeepSeekReasoningLevel,
            triage ? 6000 : chat ? ChatMaxOutputTokens : MaxOutputTokens,
            task == LlmTask.Critic ? CriticTimeoutSeconds : TimeoutSeconds,
            glm ? GlmInputEstimateRubPerMillion : DeepSeekInputEstimateRubPerMillion,
            glm ? GlmOutputEstimateRubPerMillion : DeepSeekOutputEstimateRubPerMillion,
            glm || !triage && DeepSeekThinkingEnabled);
    }

    public void Validate()
    {
        if (DailyLimitRub is <= 0 or > 500 || MaxOutputTokens is <= 0 or > 32000
            || ChatMaxOutputTokens is <= 0 or > 8000 || TimeoutSeconds <= 0 || CriticTimeoutSeconds <= 0
            || MaxInputBytes <= 0 || ImageInputTokenReserve <= 0 || ReserveOutputTokens <= 0 || MaxParallelRequests is < 1 or > 16
            || DeepSeekInputEstimateRubPerMillion < 0 || DeepSeekOutputEstimateRubPerMillion < 0
            || GlmInputEstimateRubPerMillion < 0 || GlmOutputEstimateRubPerMillion < 0
            || CriticScope is not ("all_full" or "top_only") || GlmReasoningLevel is not ("low" or "high")
            || string.IsNullOrWhiteSpace(ConfigVersion))
            throw new InvalidOperationException("Некорректные лимиты Llm.");
        if (DeepSeekModel != "deepseek/deepseek-v4.1-flash" || GlmModel != "z-ai/glm-5.3-flash")
            throw new InvalidOperationException("Разрешены только согласованные DeepSeek и GLM.");
    }
}

public sealed record LlmRoute(string Model, string Reasoning, int MaxOutputTokens, int TimeoutSeconds,
    decimal InputRubPerMillion, decimal OutputRubPerMillion, bool ThinkingEnabled = true);