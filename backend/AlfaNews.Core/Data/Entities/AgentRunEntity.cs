using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class AgentRunEntity
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid? InsightId { get; set; }
    public string InputVersion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}