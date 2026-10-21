using AlfaNews.Contracts.Domain;

namespace AlfaNews.Contracts.Processing;

public sealed record ToolResult(IReadOnlyDictionary<string, EvidenceDto> Evidence, IReadOnlyList<string> Limitations);