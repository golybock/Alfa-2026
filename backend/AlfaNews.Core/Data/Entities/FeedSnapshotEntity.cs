using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class FeedSnapshotEntity
{
    public string CompanyId { get; set; } = string.Empty;
    public DateTimeOffset AsOf { get; set; }
    public DateTimeOffset NewsPeriodFrom { get; set; }
    public DateTimeOffset NewsPeriodTo { get; set; }
    public string InputVersion { get; set; } = string.Empty;
    public JsonElement ItemsJson { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
    public DateTimeOffset? CollectionStartedAt { get; set; }
    public string RefreshStatus { get; set; } = string.Empty;
}