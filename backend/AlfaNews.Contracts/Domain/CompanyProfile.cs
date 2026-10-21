namespace AlfaNews.Contracts.Domain;

public sealed record CompanyProfile(
	string CompanyId,
	string? GroupId,
	string OkvedCode,
	string Okved2,
	string? IndustryName,
	string? Region,
	string? City,
	string? AgeBand,
	string? Lifecycle,
	decimal? Revenue,
	IReadOnlyList<string> Products,
	IReadOnlyList<string> FtsCountries,
	PaymentExposure Incoming,
	PaymentExposure Outgoing,
	string DataVersion
);