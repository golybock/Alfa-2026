using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class SourceEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public JsonElement AllowedHosts { get; set; }
    public bool Enabled { get; set; } = false;
    public bool IsSpecialized { get; set; }
    public int PollSeconds { get; set; } = 3600;
    public decimal Reliability { get; set; }
    public string ValidationStatus { get; set; } = string.Empty;
    public DateTimeOffset? LastCheckedAt { get; set; }
    public DateTimeOffset? LastSuccessAt { get; set; }
    public string? LastError { get; set; }
}