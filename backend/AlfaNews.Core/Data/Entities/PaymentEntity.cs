using System.Text.Json;
using NpgsqlTypes;
using Pgvector;

namespace AlfaNews.Core.Data.Entities;

public sealed class PaymentEntity
{
    public string Payer { get; set; } = string.Empty;
    public string Beneficiary { get; set; } = string.Empty;
    public decimal PayAmt { get; set; }
    public long PayCnt { get; set; }
    public string DatasetVersion { get; set; } = string.Empty;
}