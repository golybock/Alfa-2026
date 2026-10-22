using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class EventTagEntity
{
    public Guid EventId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}