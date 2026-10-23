using System.Text.Json;
using AlfaNews.Contracts;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Access;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Llm;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Feed;

public sealed record CompanyRankingContext(CompanyProfile Company, IReadOnlyList<CompanyProfile> GroupMembers, RankingTaxonomy Taxonomy);

public sealed class FeedRefreshService(AlfaNewsDbContext db, JobQueue queue, RankingOptions options, TimeProvider clock)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly Role[] Roles = [Role.Ceo, Role.Cfo];

    public async Task<JobResult> RefreshAsync(RefreshFeedJob payload, CancellationToken cancellationToken)
    {
        var context = await LoadContextAsync(payload.CompanyId, cancellationToken);
        if (context is null)
            return new JobResult("missing", ErrorCode: "profile_not_found");
        var asOf = payload.AsOf.ToUniversalTime();
        var from = FeedRanking.WindowStart(asOf).ToUniversalTime();
        var candidates = await LoadCandidatesAsync(from, asOf, null, cancellationToken);
        var selected = FeedRanking.Select(candidates.Select(candidate =>
            FeedRanking.Rank(candidate, context.Company, context.GroupMembers, context.Taxonomy, options, asOf)), options.MaxItems);
        var items = selected.Select((item, index) => new FeedItemDto(item.Candidate.EventId.ToString(), item.Candidate.Headline,
            item.Candidate.Summary, item.Candidate.Source, new RelevanceDto(item.Score, index + 1, item.Reasons, item.Contributions),
            item.Evidence, AnalysisStatus.Queued, null, null)).ToArray();
        var inputVersion = LlmPromptCatalog.Hash(JsonSerializer.Serialize(new
        {
            options.ConfigVersion,
            taxonomy = context.Taxonomy.Version,
            profile = context.Company.DataVersion,
            events = candidates.Select(candidate => candidate.EventId + ":" + candidate.InputVersion).Order(StringComparer.Ordinal)
        }));
        var collectionStartedAt = await db.NewsRawRows.AsNoTracking().MinAsync(row => (DateTimeOffset?)row.FetchedAt, cancellationToken);
        var now = clock.GetUtcNow();
        var snapshot = await db.FeedSnapshotRows.SingleOrDefaultAsync(row => row.CompanyId == payload.CompanyId, cancellationToken);
        if (snapshot is null)
        {
            snapshot = new FeedSnapshotEntity { CompanyId = payload.CompanyId };
            db.FeedSnapshotRows.Add(snapshot);
        }
        snapshot.AsOf = asOf;
        snapshot.NewsPeriodFrom = from;
        snapshot.NewsPeriodTo = asOf;
        snapshot.InputVersion = inputVersion;
        snapshot.ItemsJson = JsonSerializer.SerializeToElement(items, JsonOptions);
        snapshot.LastUpdatedAt = now;
        snapshot.CollectionStartedAt = collectionStartedAt;
        snapshot.RefreshStatus = "idle";
        await db.SaveChangesAsync(cancellationToken);
        if (options.PrecomputeInsights)
            await EnqueueInsightsAsync(payload.CompanyId, selected, cancellationToken);
        return new JobResult("ranked", "feed", payload.CompanyId);
    }

    public async Task<int> EnqueueForEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        if (options.CompanyIds.Count == 0)
            return 0;
        var now = clock.GetUtcNow();
        var candidate = (await LoadCandidatesAsync(DateTimeOffset.MinValue, DateTimeOffset.MaxValue, eventId, cancellationToken)).SingleOrDefault();
        if (candidate is null || candidate.Source.PublishedAt < FeedRanking.WindowStart(now) || candidate.Source.PublishedAt > now)
            return 0;
        var refreshKind = JobKinds.Name<RefreshFeedJob>();
        var queued = (await db.BackgroundJobRows.AsNoTracking().Where(row => row.Kind == refreshKind && row.Status == "queued")
            .Select(row => row.CompanyId).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var enqueued = 0;
        foreach (var companyId in options.CompanyIds)
        {
            if (queued.Contains(companyId) || await LoadContextAsync(companyId, cancellationToken) is not { } context
                || !FeedRanking.Rank(candidate, context.Company, context.GroupMembers, context.Taxonomy, options, now).Eligible)
                continue;
            await queue.EnqueueAsync(new RefreshFeedJob(companyId, now), null, companyId, cancellationToken);
            enqueued++;
        }
        return enqueued;
    }

    public async Task<RankedCandidate?> RankEventAsync(string companyId, Guid eventId, DateTimeOffset asOf,
        CancellationToken cancellationToken)
    {
        var context = await LoadContextAsync(companyId, cancellationToken);
        if (context is null)
            return null;
        var candidate = (await LoadCandidatesAsync(DateTimeOffset.MinValue, DateTimeOffset.MaxValue, eventId, cancellationToken)).SingleOrDefault();
        return candidate is null ? null
            : FeedRanking.Rank(candidate, context.Company, context.GroupMembers, context.Taxonomy, options, asOf.ToUniversalTime());
    }

    public async Task<CompanyRankingContext?> LoadContextAsync(string companyId, CancellationToken cancellationToken)
    {
        var profileJson = await db.CompanyProfileRows.AsNoTracking().Where(row => row.CompanyId == companyId)
            .Select(row => (JsonElement?)row.ProfileJson).SingleOrDefaultAsync(cancellationToken);
        if (profileJson is null)
            return null;
        var company = profileJson.Value.Deserialize<CompanyProfile>(JsonOptions)!;
        IReadOnlyList<CompanyProfile> members = [];
        if (company.GroupId is not null)
        {
            var groupJson = await db.GroupProfileRows.AsNoTracking().Where(row => row.GroupId == company.GroupId)
                .Select(row => (JsonElement?)row.ProfileJson).SingleOrDefaultAsync(cancellationToken);
            var memberIds = groupJson?.Deserialize<GroupProfile>(JsonOptions)!.MemberIds.Where(id => id != companyId).ToArray() ?? [];
            members = (await db.CompanyProfileRows.AsNoTracking().Where(row => memberIds.Contains(row.CompanyId))
                    .OrderBy(row => row.CompanyId).Select(row => row.ProfileJson).ToListAsync(cancellationToken))
                .Select(json => json.Deserialize<CompanyProfile>(JsonOptions)!).ToArray();
        }
        var factors = await db.FactorRows.AsNoTracking().OrderBy(row => row.Code)
            .Select(row => new { row.Code, row.Name, row.Version }).ToListAsync(cancellationToken);
        var sensitivity = await db.SectorSensitivityRows.AsNoTracking().OrderBy(row => row.Okved2).ThenBy(row => row.FactorCode)
            .Select(row => new SensitivityRule(row.Okved2, row.FactorCode, row.Weight, row.Channel)).ToListAsync(cancellationToken);
        var rules = await db.ProductFactorRuleRows.AsNoTracking().OrderBy(row => row.ProductCode).ThenBy(row => row.FactorCode)
            .Select(row => new ProductRule(row.ProductCode, row.FactorCode, row.Channel)).ToListAsync(cancellationToken);
        var version = LlmPromptCatalog.Hash(JsonSerializer.Serialize(new { factors, sensitivity, rules }));
        return new CompanyRankingContext(company, members, new RankingTaxonomy(
            factors.ToDictionary(row => row.Code, row => row.Name, StringComparer.Ordinal), sensitivity, rules, version));
    }

    private async Task<IReadOnlyList<FeedCandidate>> LoadCandidatesAsync(DateTimeOffset periodFrom, DateTimeOffset periodTo, Guid? eventId,
        CancellationToken cancellationToken)
    {
        var query = from item in db.EventRows.AsNoTracking()
                    join news in db.NewsRawRows.AsNoTracking() on item.NewsRawId equals news.Id
                    join source in db.SourceRows.AsNoTracking() on news.SourceId equals source.Id
                    where item.PublishedAt >= periodFrom && item.PublishedAt <= periodTo
                    select new { item.Id, item.Headline, item.ExtractionJson, item.PublishedAt, item.InputVersion, news.CanonicalUrl, source.Name, source.Reliability };
        if (eventId is { } id)
            query = query.Where(row => row.Id == id);
        var rows = await query.ToListAsync(cancellationToken);
        var ids = rows.Select(row => row.Id).ToArray();
        var tags = (await db.EventTagRows.AsNoTracking().Where(tag => ids.Contains(tag.EventId)).ToListAsync(cancellationToken))
            .ToLookup(tag => tag.EventId);
        var candidates = new List<FeedCandidate>();
        foreach (var row in rows)
        {
            if (ReadExtraction(row.ExtractionJson) is not { } extraction)
                continue;
            IReadOnlySet<string> Codes(string kind) => tags[row.Id].Where(tag => tag.Kind == kind).Select(tag => tag.Code).ToHashSet(StringComparer.Ordinal);
            candidates.Add(new FeedCandidate(row.Id, row.Headline, string.Join(" ", extraction.Facts.Take(2).Select(fact => fact.Text)),
                new SourceSummaryDto(row.Name, row.CanonicalUrl, row.PublishedAt), extraction.Facts.FirstOrDefault()?.Quote,
                row.Reliability, row.InputVersion, Codes("factor"), Codes("okved2"), Codes("country")));
        }
        return candidates;
    }

    private static NewsExtraction? ReadExtraction(JsonElement json)
    {
        try
        {
            return json.Deserialize<NewsExtraction>(LlmOutputValidator.JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task EnqueueInsightsAsync(string companyId, IReadOnlyList<RankedCandidate> selected, CancellationToken cancellationToken)
    {
        foreach (var item in selected)
            foreach (var role in Roles)
            {
                var roleName = AccessService.RoleName(role);
                if (await db.InsightRows.AsNoTracking().AnyAsync(row => row.CompanyId == companyId && row.EventId == item.Candidate.EventId
                        && row.Role == roleName && row.InputVersion == item.Candidate.InputVersion, cancellationToken))
                    continue;
                await queue.EnqueueAsync(new GenerateInsightJob(companyId, item.Candidate.EventId.ToString(), role, item.Candidate.InputVersion),
                    null, companyId, cancellationToken);
            }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions();
        ContractJson.Configure(options);
        return options;
    }
}