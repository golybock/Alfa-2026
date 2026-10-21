namespace AlfaNews.Contracts.Domain;

public sealed record CriticVerificationDto(CriticStatus Status, int RequestCount, string? Reason);