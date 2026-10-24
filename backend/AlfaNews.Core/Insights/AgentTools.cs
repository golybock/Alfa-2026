using System.Globalization;
using System.Text.Json;
using AlfaNews.Contracts;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Documents;
using AlfaNews.Core.Feed;
using AlfaNews.Core.Ingest;
using AlfaNews.Core.Profiles;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Insights;

public sealed class EvidenceRegistry
{
    private readonly Dictionary<string, EvidenceDto> items = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> identities = new(StringComparer.Ordinal);

    public EvidenceRegistry(IReadOnlyDictionary<string, EvidenceDto> initial)
    {
        foreach (var (id, evidence) in initial.OrderBy(pair => int.Parse(pair.Key[1..], CultureInfo.InvariantCulture)))
        {
            items[id] = evidence;
            identities.TryAdd(Identity(evidence), id);
        }
    }

    public IReadOnlyDictionary<string, EvidenceDto> Items => items;

    public string Add(EvidenceDto evidence)
    {
        var identity = Identity(evidence);
        if (identities.TryGetValue(identity, out var existing))
            return existing;
        var id = "E" + (items.Count + 1).ToString(CultureInfo.InvariantCulture);
        items[id] = evidence;
        identities[identity] = id;
        return id;
    }

    private static string Identity(EvidenceDto evidence) => JsonSerializer.Serialize(evidence with { Source = "" }, AgentTools.JsonOptions);
}

public sealed record ToolExecution(string Name, JsonElement Arguments, IReadOnlyList<string> EvidenceIds, IReadOnlyList<string> Limitations, string Summary);

public sealed record AgentScope(CompanyProfile Company, NewsExtraction News, Guid EventId, DateTimeOffset AsOf);

public sealed class AgentTools(AlfaNewsDbContext db, ResearchSearch research, NewsSearch newsSearch)
{
    // Подсказка модели при поиске обзоров; на экран карточки не попадает — там отметку ставит код, если обзор процитирован.
    public const string UnreviewedBankText = "Фрагменты аналитики банка распознаны автоматически и не проверены вручную.";

    public const string EventTool = "get_event";
    public const string BankSearch = "search_bank_research";
    public const string SimilarNews = "search_similar_news";
    private const int ContextQuoteLength = 600;
    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    public static IReadOnlyDictionary<string, JsonElement> Definitions { get; } = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
    {
        ["get_company_profile"] = Schema("""{"type":"object","properties":{},"additionalProperties":false}"""),
        ["get_event"] = Schema("""{"type":"object","properties":{},"additionalProperties":false}"""),
        ["get_group_summary"] = Schema("""{"type":"object","properties":{},"additionalProperties":false}"""),
        ["get_payment_exposure"] = Schema("""{"type":"object","properties":{"direction":{"enum":["incoming","outgoing"]},"okved2":{"type":["string","null"],"pattern":"^[0-9]{2}$"}},"required":["direction","okved2"],"additionalProperties":false}"""),
        [BankSearch] = Schema("""{"type":"object","properties":{"query":{"type":"string","minLength":1,"maxLength":500},"factors":{"type":"array","items":{"type":"string"},"maxItems":10},"limit":{"type":"integer","minimum":1,"maximum":6}},"required":["query","factors","limit"],"additionalProperties":false}"""),
        [SimilarNews] = Schema("""{"type":"object","properties":{"query":{"type":"string","minLength":1,"maxLength":500},"limit":{"type":"integer","minimum":1,"maximum":5}},"required":["query","limit"],"additionalProperties":false}""")
    };

    public static IReadOnlyDictionary<string, EvidenceDto> ForContext(IReadOnlyDictionary<string, EvidenceDto> evidence,
        IReadOnlySet<string>? only = null) => evidence.Where(pair => only is null || only.Contains(pair.Key))
        .ToDictionary(pair => pair.Key, pair => pair.Value.Quote is { Length: > ContextQuoteLength } quote
            ? pair.Value with { Quote = quote[..ContextQuoteLength].TrimEnd() + "…" } : pair.Value, StringComparer.Ordinal);

    public async Task<ToolExecution> ExecuteAsync(ToolCall call, AgentScope scope, EvidenceRegistry registry, CancellationToken cancellationToken)
    {
        var company = scope.Company;
        var news = scope.News;
        var ids = new List<string>();
        var limitations = new List<string>();
        string summary;
        switch (call.Name)
        {
            case "get_company_profile":
                ids.Add(registry.Add(Feature(company, "okved2", new { code = company.Okved2, name = Okved2Names.Find(company.Okved2) ?? company.IndustryName })));
                ids.Add(registry.Add(Feature(company, "products", company.Products)));
                ids.Add(registry.Add(Feature(company, "fts_countries", company.FtsCountries)));
                if (company.Region is not null)
                    ids.Add(registry.Add(Feature(company, "region", company.Region)));
                if (company.Lifecycle is not null)
                    ids.Add(registry.Add(Feature(company, "lifecycle", company.Lifecycle)));
                limitations.Add("В данных нет долговой нагрузки, условий кредитных договоров, структуры затрат и выручки по направлениям.");
                if (company.FtsCountries.Count == 0)
                    limitations.Add("Страны ФТС по компании не указаны; это не доказывает отсутствие внешней торговли.");
                summary = "Признаки компании из профиля.";
                break;
            case EventTool:
                var source = registry.Items["E1"];
                foreach (var fact in news.Facts.Where(fact => IsUsefulQuote(fact.Quote)))
                    ids.Add(registry.Add(source with { Quote = fact.Quote }));
                if (ids.Count == 0)
                    limitations.Add("В тексте новости не найдено подтверждаемых фрагментов, кроме заголовка.");
                summary = $"Фактов события с цитатами: {ids.Distinct().Count()}.";
                break;
            case SimilarNews:
                var similarQuery = call.Arguments.GetProperty("query").GetString()!;
                var hits = await newsSearch.SearchAsync(similarQuery, scope.EventId, FeedRanking.WindowStart(scope.AsOf).ToUniversalTime(),
                    scope.AsOf.ToUniversalTime(), call.Arguments.GetProperty("limit").GetInt32(), cancellationToken);
                foreach (var hit in hits)
                    ids.Add(registry.Add(new EvidenceDto(EvidenceType.News, hit.SourceName, hit.Url, hit.Quote ?? hit.Headline,
                        DateOnly.FromDateTime(hit.PublishedAt.ToOffset(TimeSpan.FromHours(3)).DateTime), null, null, null, "similar_news",
                        JsonSerializer.SerializeToElement(new { headline = hit.Headline }), null, null, false, null, null)));
                if (hits.Count == 0)
                    limitations.Add("Похожих новостей за последние семь дней не найдено.");
                summary = $"Найдено похожих новостей: {hits.Count}.";
                break;
            case "get_group_summary":
                summary = await GroupAsync(company, registry, ids, limitations, cancellationToken);
                break;
            case "get_payment_exposure":
                var payment = call.Arguments.Deserialize<GetPaymentArguments>(JsonOptions)!;
                var exposure = payment.Direction == PaymentDirection.Incoming ? company.Incoming : company.Outgoing;
                var direction = payment.Direction == PaymentDirection.Incoming ? "incoming" : "outgoing";
                var shares = payment.Okved2 is { } code
                    ? exposure.SectorShares.Where(pair => pair.Key == code)
                    : exposure.SectorShares.OrderByDescending(pair => pair.Value).Take(5);
                ids.Add(registry.Add(new EvidenceDto(EvidenceType.PaymentAggregate, "Платёжные агрегаты компании", null, null, null, null, null, null,
                    direction + ".sector_shares", JsonSerializer.SerializeToElement(new
                    {
                        direction,
                        coverage = Math.Round(exposure.Coverage, 6),
                        shares = shares.ToDictionary(pair => pair.Key, pair => Math.Round(pair.Value, 6))
                    }), company.DataVersion, null, false, null, null)));
                limitations.Add("Доли рассчитаны по наблюдаемому внешнему платёжному обороту за 01.01–31.10.2026 без самопереводов; они не подтверждают поставки, закупки или тип отношений.");
                if (exposure.Coverage < 1)
                    limitations.Add($"Покрытие профилями по направлению {(direction == "incoming" ? "входящих" : "исходящих")} платежей — {Math.Round(exposure.Coverage * 100).ToString("0", CultureInfo.InvariantCulture)}%; остальной оборот приходится на участников без карточки.");
                summary = "Платёжные доли по направлению " + direction + ".";
                break;
            case BankSearch:
                var search = call.Arguments.Deserialize<SearchResearchArguments>(JsonOptions)!;
                var allowed = news.Tags.Factors.ToHashSet(StringComparer.Ordinal);
                var found = await research.SearchAsync(search with { Factors = search.Factors.Where(allowed.Contains).ToArray() }, cancellationToken);
                foreach (var hit in found)
                    ids.Add(registry.Add(new EvidenceDto(EvidenceType.BankResearch, hit.Title, null, hit.Text, hit.PublishedOn,
                        hit.DocumentId.ToString(), hit.PageNo, hit.ChunkId.ToString(), JsonSerializer.Serialize(hit.Kind, JsonOptions).Trim('"'),
                        hit.ReviewedCharts.Count > 0 ? JsonSerializer.SerializeToElement(hit.ReviewedCharts, JsonOptions) : null, null,
                        hit.ReviewStatus, false, null, $"/api/research/{hit.DocumentId}/pages/{hit.PageNo}")));
                if (found.Any(hit => hit.ReviewStatus != ReviewStatus.Reviewed))
                    limitations.Add(UnreviewedBankText);
                if (found.Count == 0)
                    limitations.Add("Подходящих материалов аналитики банка по теме события не найдено.");
                summary = $"Найдено фрагментов аналитики банка: {found.Count}.";
                break;
            default:
                throw new InvalidOperationException("Неизвестный инструмент агента.");
        }
        return new ToolExecution(call.Name, call.Arguments, ids.Distinct(StringComparer.Ordinal).ToArray(), limitations, summary);
    }

    private static bool IsUsefulQuote(string quote) => quote.Any(char.IsDigit)
        || quote.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Count(word => word.Any(char.IsLetter)) >= 5;

    private async Task<string> GroupAsync(CompanyProfile company, EvidenceRegistry registry, List<string> ids, List<string> limitations,
        CancellationToken cancellationToken)
    {
        if (company.GroupId is null)
        {
            limitations.Add("Компания не входит в группу по GROUP_ID в доступных данных.");
            return "Группа не указана.";
        }
        var groupJson = await db.GroupProfileRows.AsNoTracking().Where(row => row.GroupId == company.GroupId)
            .Select(row => (JsonElement?)row.ProfileJson).SingleOrDefaultAsync(cancellationToken);
        var group = groupJson?.Deserialize<GroupProfile>(JsonOptions);
        var memberIds = group?.MemberIds.Where(id => id != company.CompanyId).ToArray() ?? [];
        var sectors = (await db.CompanyProfileRows.AsNoTracking().Where(row => memberIds.Contains(row.CompanyId))
                .Select(row => row.ProfileJson).ToListAsync(cancellationToken))
            .Select(json => json.Deserialize<CompanyProfile>(JsonOptions)!.Okved2).Where(code => code.Length > 0)
            .GroupBy(code => code).OrderBy(code => code.Key, StringComparer.Ordinal)
            .ToDictionary(code => code.Key, code => code.Count());
        ids.Add(registry.Add(new EvidenceDto(EvidenceType.GroupFeature, "Данные группы компании", null, null, null, null, null, null,
            "group_summary", JsonSerializer.SerializeToElement(new { other_members = memberIds.Length, member_okved2 = sectors }),
            group?.DataVersion ?? company.DataVersion, null, false, null, null)));
        limitations.Add("Состав группы известен только по GROUP_ID среди отфильтрованных компаний-клиентов; связи через физлиц не учитываются.");
        return $"Других компаний группы в данных: {memberIds.Length}.";
    }

    private static EvidenceDto Feature(CompanyProfile company, string key, object value) => new(EvidenceType.CompanyFeature, "Данные компании",
        null, null, null, null, null, null, key, JsonSerializer.SerializeToElement(value), company.DataVersion, null, false, null, null);

    private static JsonElement Schema(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions();
        ContractJson.Configure(options);
        return options;
    }
}