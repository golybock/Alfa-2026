namespace AlfaNews.Contracts.Domain;

public sealed record GroupProfile(
	string GroupId,
	IReadOnlyList<string> MemberIds,
	decimal? KnownRevenueSum,
	int RevenueKnownCount,
	string DataVersion
);