namespace AlfaNews.Contracts.Processing;

public sealed record CollectedNews(
	string SourceId,
	string CanonicalUrl,
	string Title,
	string Text,
	DateTimeOffset? PublishedAt,
	DateTimeOffset FetchedAt,
	string TextSha256
);