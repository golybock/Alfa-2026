namespace AlfaNews.Contracts.Api;

public sealed record ModelConfigResponse(
    IReadOnlyDictionary<string, string> Models, bool ThinkingEnabled, string GlmReasoning, int GeneralMaxTokens,
    int ChatMaxTokens, bool CriticEnabled, string CriticScope);