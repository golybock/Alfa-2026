using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class ResearchDocEntity
{
    public Guid Id { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public DateOnly? PublishedOn { get; set; }
    public int PageCount { get; set; }
    public string OcrStatus { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public Guid? UploadedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}