using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class FactorEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JsonElement Keywords { get; set; } = JsonSerializer.SerializeToElement(Array.Empty<object>());
    public string Version { get; set; } = string.Empty;
}