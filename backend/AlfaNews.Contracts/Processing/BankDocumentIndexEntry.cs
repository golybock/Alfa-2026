using AlfaNews.Contracts.Domain;

namespace AlfaNews.Contracts.Processing;

public sealed record BankDocumentIndexEntry(
	string FilePath,
	string Sha256,
	string? Title,
	DateOnly? PublishedOn,
	string? SourceUrl,
	int PageCount,
	string TextLayer,
	ReviewStatus ReviewStatus
);