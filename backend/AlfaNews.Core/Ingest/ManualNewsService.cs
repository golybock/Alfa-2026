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
    public const int MaxListItems = 50;

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

    // Ссылки, которые пользователь отправил сам, новые сверху; заголовок — из собранной статьи, если она уже есть в базе.
    public async Task<ManualNewsListResponse> ListAsync(Guid userId, string requestId, CancellationToken cancellationToken)
    {
        var kind = JobKinds.Name<CollectManualNewsJob>();
        var jobs = await db.BackgroundJobRows.AsNoTracking().Where(row => row.Kind == kind && row.OwnerUserId == userId)
            .OrderByDescending(row => row.CreatedAt).Take(MaxListItems).ToArrayAsync(cancellationToken);
        var urls = jobs.Select(job => (Job: job, Url: job.Payload.Deserialize<CollectManualNewsJob>(JobKinds.JsonOptions)!.Request.Url ?? string.Empty))
            .Select(item => (item.Job, item.Url, Canonical: Uri.TryCreate(item.Url, UriKind.Absolute, out var uri) ? TextNormalizer.CanonicalUrl(uri) : item.Url))
            .ToArray();
        var canonical = urls.Select(item => item.Canonical).ToArray();
        var titles = await db.NewsRawRows.AsNoTracking().Where(row => canonical.Contains(row.CanonicalUrl))
            .ToDictionaryAsync(row => row.CanonicalUrl, row => row.Title, cancellationToken);
        return new ManualNewsListResponse(urls.Select(item =>
        {
            var job = JobQueue.ToResponse(item.Job, requestId);
            return new ManualNewsItemDto(item.Job.Id.ToString(), item.Url, titles.GetValueOrDefault(item.Canonical), job.Status, job.Stage,
                job.Error, item.Job.CreatedAt);
        }).ToArray());
    }

    public static string[] Hosts(SourceEntity source) => source.AllowedHosts.Deserialize<string[]>() ?? [];
}