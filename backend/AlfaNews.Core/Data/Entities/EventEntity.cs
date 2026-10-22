using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class EventEntity
{
    public Guid Id { get; set; }
    public Guid NewsRawId { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public DateOnly? EventDate { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public JsonElement ExtractionJson { get; set; }
    public string InputVersion { get; set; } = string.Empty;
    public required NpgsqlTsVector SearchText { get; set; }
    public Vector? Embedding { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}