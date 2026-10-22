using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class ProductFactorRuleEntity
{
    public string ProductCode { get; set; } = string.Empty;
    public string FactorCode { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}