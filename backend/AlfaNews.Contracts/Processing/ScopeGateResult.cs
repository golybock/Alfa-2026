using AlfaNews.Contracts.Domain;

namespace AlfaNews.Contracts.Processing;

public sealed record ScopeGateResult(ScopeLabel Label, string Reason);