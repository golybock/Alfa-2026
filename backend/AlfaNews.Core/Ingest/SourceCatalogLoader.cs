using System.Text.Json;
using AlfaNews.Contracts;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Ingest;

public sealed record SourceCatalogEntry(
    string Id,
    string Name,
    string FeedUrl,
    string Kind,
    string Rubric,
    string Publisher,
    IReadOnlyList<string> AllowedHosts,
    bool Enabled,
    int PollSeconds,
    decimal Reliability,
    string ValidationStatus,
    DateTimeOffset? CheckedAt,
    string SelectionReason);

public sealed class SourceCatalogLoader(AlfaNewsDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<int> LoadAsync(string path, CancellationToken cancellationToken, bool? isSpecialized = null)
    {
        var entries = JsonSerializer.Deserialize<List<SourceCatalogEntry>>(
            await File.ReadAllTextAsync(path, cancellationToken), JsonOptions)
            ?? throw new InvalidOperationException("Каталог источников пуст.");
        var existing = await db.SourceRows.ToDictionaryAsync(row => row.Id, cancellationToken);
        foreach (var entry in entries)
        {
            if (!existing.TryGetValue(entry.Id, out var row))
            {
                row = new SourceEntity { Id = entry.Id };
                db.SourceRows.Add(row);
            }
            row.Name = entry.Name;
            row.FeedUrl = entry.FeedUrl;
            row.Kind = entry.Kind;
            row.AllowedHosts = JsonSerializer.SerializeToElement(entry.AllowedHosts);
            row.Enabled = entry.Enabled && entry.ValidationStatus == "checked";
            row.IsSpecialized = isSpecialized ?? Path.GetFileName(path).Equals("sources-specialized.json", StringComparison.OrdinalIgnoreCase);
            row.PollSeconds = row.IsSpecialized ? 900 : 3600;
            row.Reliability = entry.Reliability;
            row.ValidationStatus = entry.ValidationStatus;
        }
        await db.SaveChangesAsync(cancellationToken);
        return entries.Count;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions();
        ContractJson.Configure(options);
        return options;
    }
}