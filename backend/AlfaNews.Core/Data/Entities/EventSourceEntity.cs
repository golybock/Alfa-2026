using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class EventSourceEntity
{
    public Guid EventId { get; set; }
    public Guid NewsRawId { get; set; }
}