using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class LlmUsageEntity
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid? CardId { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ConfigVersion { get; set; } = string.Empty;
    public int Attempt { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long? ReasoningTokens { get; set; }
    public decimal? ActualCostRub { get; set; }
    public long DurationMs { get; set; }
    public bool CacheHit { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}