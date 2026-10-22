using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class CompanyProfileEntity
{
    public string CompanyId { get; set; } = string.Empty;
    public string DataVersion { get; set; } = string.Empty;
    public JsonElement ProfileJson { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}