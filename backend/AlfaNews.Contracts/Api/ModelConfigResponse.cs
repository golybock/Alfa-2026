namespace AlfaNews.Contracts.Api;

public sealed record ModelConfigResponse(
    string DeepSeekModel, string GlmModel, string GlmReasoning, int GeneralMaxTokens,
    int ChatMaxTokens, bool CriticEnabled, string CriticScope);