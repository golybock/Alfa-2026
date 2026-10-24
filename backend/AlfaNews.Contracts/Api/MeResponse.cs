namespace AlfaNews.Contracts.Api;

public sealed record MeResponse(string UserId, string DisplayName, bool IsOperator, IReadOnlyList<CompanyAccessDto> Companies,
    bool CanAddMaterials);