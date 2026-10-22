using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class FeedbackEntity
{
    public Guid UserId { get; set; }
    public string CompanyId { get; set; } = string.Empty;
    public Guid EventId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}