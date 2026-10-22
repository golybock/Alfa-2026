using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class InsightEntity
{
    public Guid Id { get; set; }
    public string CompanyId { get; set; } = string.Empty;
    public Guid EventId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string InputVersion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public JsonElement? BodyJson { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}