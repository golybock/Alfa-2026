using AlfaNews.Contracts.Domain;

namespace AlfaNews.Contracts.Api;

public sealed record UserActivityRequest(
    string EventId,
    UserActivityType Type,
    string? EvidenceId,
    string? ActionId
);