using System.Text.Json;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Access;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Ingest;

// D17: новость по ссылке принимается только с хоста включённого источника — allowlist не обходится. Источник берётся из
// source_id или подбирается по хосту ссылки. Ручной текст без ссылки не принимается: у новости должен быть первоисточник.
public sealed class ManualNewsService(AlfaNewsDbContext db, JobQueue queue)
{
    public const int MaxUrlLength = 2000;

    public async Task<BackgroundJobEntity> EnqueueAsync(ManualNewsRequest request, Guid userId, CancellationToken cancellationToken)
    {
        var (sourceId, url) = await ResolveAsync(request, cancellationToken);
        return await queue.EnqueueAsync(new CollectManualNewsJob(new ManualNewsRequest(sourceId, url, null, null, null)), userId, null,
            cancellationToken);
    }

    public async Task<(string SourceId, string Url)> ResolveAsync(ManualNewsRequest request, CancellationToken cancellationToken)
    {
        var text = request.Url?.Trim();
        if (string.IsNullOrEmpty(text))
            throw new AccessException(400, "invalid_input", "Укажите ссылку на новость. Текст без ссылки не принимается.");
        if (text.Length > MaxUrlLength || !Uri.TryCreate(text, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
            throw new AccessException(400, "invalid_input", "Некорректная ссылка на новость.");
        var sources = await db.SourceRows.AsNoTracking().Where(row => row.Enabled).OrderBy(row => row.Id).ToListAsync(cancellationToken);
        if (request.SourceId is not null && sources.All(source => source.Id != request.SourceId))
            throw new AccessException(400, "invalid_input", "Источник не найден или отключён.");
        var source = sources.FirstOrDefault(source => (request.SourceId is null || source.Id == request.SourceId)
            && Hosts(source).Contains(url.Host, StringComparer.OrdinalIgnoreCase));
        if (source is null)
        {
            var sites = sources.SelectMany(Hosts).Where(host => !host.StartsWith("rss", StringComparison.OrdinalIgnoreCase))
                .Select(host => host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host)
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal);
            throw new AccessException(400, "invalid_input", $"Ссылка не с сайта подключённого источника. Можно добавить новость с сайтов: {string.Join(", ", sites)}.");
        }
        return (source.Id, url.ToString());
    }

    public static string[] Hosts(SourceEntity source) => source.AllowedHosts.Deserialize<string[]>() ?? [];
}