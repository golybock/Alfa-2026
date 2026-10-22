using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class CompanyEntity
{
    public string Id { get; set; } = string.Empty;
    public string ClType { get; set; } = string.Empty;
    public string? GroupId { get; set; }
    public string MainOkvedCode { get; set; } = string.Empty;
    public string? MainOkvedName { get; set; }
    public string? Region { get; set; }
    public string? City { get; set; }
    public decimal? Revenue { get; set; }
    public JsonElement RawData { get; set; }
    public string DatasetVersion { get; set; } = string.Empty;
    public JsonElement QualityFlags { get; set; } = JsonSerializer.SerializeToElement(Array.Empty<object>());
    public DateTimeOffset ImportedAt { get; set; }
}