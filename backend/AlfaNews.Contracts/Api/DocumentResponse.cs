using AlfaNews.Contracts.Domain;

namespace AlfaNews.Contracts.Api;

public sealed record DocumentResponse(
	string Id,
	string Title,
	DateOnly? PublishedOn,
	int PageCount,
	string Version,
	OcrStatus OcrStatus,
	ReviewStatus ReviewStatus,
	IReadOnlyList<DocumentPageDto> Pages,
	bool LlmKeyRequired
);