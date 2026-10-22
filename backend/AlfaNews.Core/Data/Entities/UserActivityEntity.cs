using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class UserActivityEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid UserId { get; set; }
    public string CompanyId { get; set; } = string.Empty;
    public Guid InsightId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string? EvidenceId { get; set; }
    public string? ActionId { get; set; }
}