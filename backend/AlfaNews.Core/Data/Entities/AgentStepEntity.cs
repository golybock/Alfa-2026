using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class AgentStepEntity
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public int Ordinal { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string? ToolName { get; set; }
    public JsonElement EvidenceRefs { get; set; } = JsonSerializer.SerializeToElement(Array.Empty<object>());
    public string Summary { get; set; } = string.Empty;
    public long DurationMs { get; set; }
}