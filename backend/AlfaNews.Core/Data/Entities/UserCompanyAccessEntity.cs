using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class UserCompanyAccessEntity
{
    public Guid UserId { get; set; }
    public string CompanyId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}