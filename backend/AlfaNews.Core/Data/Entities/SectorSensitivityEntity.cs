using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class SectorSensitivityEntity
{
    public string Okved2 { get; set; } = string.Empty;
    public string FactorCode { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public string Sign { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string ReviewedBy { get; set; } = string.Empty;
    public DateTimeOffset ReviewedAt { get; set; }
    public string Version { get; set; } = string.Empty;
}