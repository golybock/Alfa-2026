namespace AlfaNews.Contracts.Api;

public sealed record CompanyAccessDto(string CompanyId, string Name, IReadOnlyList<Role> Roles);