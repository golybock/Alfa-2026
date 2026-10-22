using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class ChatMessageEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid InsightId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public JsonElement? AnswerJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}