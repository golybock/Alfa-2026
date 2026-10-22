using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class ResearchChunkEntity
{
    public Guid Id { get; set; }
    public Guid PageId { get; set; }
    public string LocalId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public JsonElement TablesJson { get; set; } = JsonSerializer.SerializeToElement(Array.Empty<object>());
    public JsonElement ChartData { get; set; } = JsonSerializer.SerializeToElement(Array.Empty<object>());
    public JsonElement TagsJson { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
    public string Version { get; set; } = string.Empty;
    public required NpgsqlTsVector SearchText { get; set; }
    public Vector? Embedding { get; set; }
}