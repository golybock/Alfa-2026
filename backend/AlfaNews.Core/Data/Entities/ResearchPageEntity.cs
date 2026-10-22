using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class ResearchPageEntity
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int PageNo { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public string OcrStatus { get; set; } = string.Empty;
    public JsonElement? OcrJson { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? LastError { get; set; }
}