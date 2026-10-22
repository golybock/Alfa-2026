using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class LlmCacheEntity
{
    public string CacheKey { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public JsonElement ResponseJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}