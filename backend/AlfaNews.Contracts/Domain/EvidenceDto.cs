using System.Text.Json;

namespace AlfaNews.Contracts.Domain;

public sealed record EvidenceDto(
	EvidenceType Type,
	string Source,
	string? Url,
	string? Quote,
	DateOnly? Date,
	string? DocumentId,
	int? Page,
	string? ChunkId,
	string? Key,
	JsonElement? Value,
	string? Dataset,
	ReviewStatus? ReviewStatus,
	bool Approximate,
	string? ChartId,
	string? PageImageUrl
);