using System.Text.Json;
using AlfaNews.Contracts;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Feed;

public sealed record TaxonomyFactorEntry(string Code, string Name, string Description, IReadOnlyList<string> Keywords);

public sealed record TaxonomyProductRuleEntry(string Product, string Factor, string Channel);

public sealed record TaxonomySensitivityEntry(
    string Okved2,
    string Factor,
    decimal Weight,
    string Sign,
    string Channel);

public sealed record TaxonomyCatalog(
    string Version,
    string ReviewedBy,
    DateTimeOffset ReviewedAt,
    IReadOnlyList<TaxonomyFactorEntry> Factors,
    IReadOnlyList<TaxonomyProductRuleEntry> ProductRules,
    IReadOnlyList<TaxonomySensitivityEntry> SectorSensitivity);

public sealed class TaxonomyCatalogLoader(AlfaNewsDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<int> LoadAsync(string path, CancellationToken cancellationToken)
    {
        var catalog =
            JsonSerializer.Deserialize<TaxonomyCatalog>(await File.ReadAllTextAsync(path, cancellationToken),
                JsonOptions)
            ?? throw new InvalidOperationException("Каталог таксономии пуст.");
        var codes = catalog.Factors.Select(factor => factor.Code).ToHashSet(StringComparer.Ordinal);
        if (codes.Count != catalog.Factors.Count
            || catalog.ProductRules.Any(rule => !codes.Contains(rule.Factor))
            || catalog.SectorSensitivity.Any(row => !codes.Contains(row.Factor) || row.Weight is < 0 or > 1
                || row.Sign is not ("positive" or "negative" or "mixed")))
            throw new InvalidOperationException(
                "Каталог таксономии содержит неизвестные коды или недопустимые значения.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.ProductFactorRuleRows.ExecuteDeleteAsync(cancellationToken);
        await db.SectorSensitivityRows.ExecuteDeleteAsync(cancellationToken);
        await db.FactorRows.Where(row => !codes.Contains(row.Code)).ExecuteDeleteAsync(cancellationToken);
        db.ChangeTracker.Clear();
        var existing = await db.FactorRows.ToDictionaryAsync(row => row.Code, cancellationToken);
        foreach (var factor in catalog.Factors)
        {
            if (!existing.TryGetValue(factor.Code, out var row))
            {
                row = new FactorEntity { Code = factor.Code };
                db.FactorRows.Add(row);
            }

            row.Name = factor.Name;
            row.Description = factor.Description;
            row.Keywords = JsonSerializer.SerializeToElement(factor.Keywords);
            row.Version = catalog.Version;
        }

        db.ProductFactorRuleRows.AddRange(catalog.ProductRules.Select(rule => new ProductFactorRuleEntity
        {
            ProductCode = rule.Product,
            FactorCode = rule.Factor,
            Channel = rule.Channel,
            Version = catalog.Version
        }));
        db.SectorSensitivityRows.AddRange(catalog.SectorSensitivity.Select(row => new SectorSensitivityEntity
        {
            Okved2 = row.Okved2,
            FactorCode = row.Factor,
            Weight = row.Weight,
            Sign = row.Sign,
            Channel = row.Channel,
            ReviewedBy = catalog.ReviewedBy,
            ReviewedAt = catalog.ReviewedAt.ToUniversalTime(),
            Version = catalog.Version
        }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return catalog.Factors.Count;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions();
        ContractJson.Configure(options);
        return options;
    }
}