using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class BackgroundJobEntity
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public JsonElement Payload { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string? CompanyId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int Attempts { get; set; } = 0;
    public string Stage { get; set; } = string.Empty;
    public string? ResultType { get; set; }
    public string? ResultId { get; set; }
    public string? LastError { get; set; }
}