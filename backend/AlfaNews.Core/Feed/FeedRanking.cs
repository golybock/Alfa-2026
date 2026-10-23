using System.Globalization;
using System.Text.Json;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Core.Profiles;

namespace AlfaNews.Core.Feed;

public sealed record SensitivityRule(string Okved2, string Factor, decimal Weight, string Channel);

public sealed record ProductRule(string Product, string Factor, string Channel);

public sealed record RankingTaxonomy(
    IReadOnlyDictionary<string, string> FactorNames,
    IReadOnlyList<SensitivityRule> Sensitivity,
    IReadOnlyList<ProductRule> ProductRules,
    string Version);

public sealed record FeedCandidate(
    Guid EventId,
    string Headline,
    string Summary,
    SourceSummaryDto Source,
    string? Quote,
    decimal SourceReliability,
    string InputVersion,
    IReadOnlySet<string> Factors,
    IReadOnlySet<string> Okved2,
    IReadOnlySet<string> Countries);

public sealed record RankedCandidate(
    FeedCandidate Candidate,
    decimal Base,
    decimal Recency,
    decimal Score,
    bool Eligible,
    IReadOnlyList<FeatureContribution> Contributions,
    IReadOnlyList<RelevanceReasonDto> Reasons,
    IReadOnlyDictionary<string, EvidenceDto> Evidence);

public static class FeedRanking
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    private static readonly IReadOnlyDictionary<string, string> ProductNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["loan"] = "кредит",
        ["leasing"] = "лизинг",
        ["deposit"] = "депозит",
        ["guarantee"] = "банковская гарантия",
        ["payroll"] = "зарплатный проект",
        ["factoring"] = "факторинг",
        ["rko"] = "расчётно-кассовое обслуживание"
    };

    public static DateTimeOffset WindowStart(DateTimeOffset asOf)
    {
        var moscow = asOf.ToOffset(TimeSpan.FromHours(3));
        return new DateTimeOffset(moscow.Date.AddDays(-6), TimeSpan.FromHours(3));
    }

    public static IReadOnlyList<RankedCandidate> Select(IEnumerable<RankedCandidate> ranked, int maxItems) => ranked
        .Where(item => item.Eligible)
        .OrderByDescending(item => item.Score).ThenByDescending(item => item.Candidate.Source.PublishedAt)
        .ThenBy(item => item.Candidate.EventId.ToString(), StringComparer.Ordinal)
        .Take(maxItems).ToArray();

    public static RankedCandidate Rank(FeedCandidate candidate, CompanyProfile company, IReadOnlyList<CompanyProfile> groupMembers,
        RankingTaxonomy taxonomy, RankingOptions options, DateTimeOffset asOf)
    {
        var evidence = new Dictionary<string, EvidenceDto>(StringComparer.Ordinal)
        {
            ["E1"] = new(EvidenceType.News, candidate.Source.Name, candidate.Source.Url, candidate.Quote,
                DateOnly.FromDateTime(candidate.Source.PublishedAt.ToOffset(TimeSpan.FromHours(3)).DateTime),
                null, null, null, null, null, null, null, false, null, null)
        };
        var added = new Dictionary<string, string>(StringComparer.Ordinal);
        string Add(EvidenceType type, string key, object value)
        {
            var json = JsonSerializer.SerializeToElement(value);
            var identity = type + ":" + key + ":" + json.GetRawText();
            if (added.TryGetValue(identity, out var existing))
                return existing;
            var id = "E" + (evidence.Count + 1).ToString(CultureInfo.InvariantCulture);
            evidence[id] = new EvidenceDto(type, type switch
            {
                EvidenceType.PaymentAggregate => "Платёжные агрегаты компании",
                EvidenceType.GroupFeature => "Данные группы компании",
                _ => "Данные компании"
            }, null, null, null, null, null, null, key, json, company.DataVersion, null, false, null, null);
            added[identity] = id;
            return id;
        }
        var reasons = new List<RelevanceReasonDto>();
        var contributions = new List<FeatureContribution>();
        void Feature(RankingFeatureCode code, decimal value, decimal weight, bool known, IReadOnlyList<string> refs) =>
            contributions.Add(new FeatureContribution(code, value, weight, Math.Round(value * weight, 6), known, refs));

        var okvedKnown = company.Okved2.Length > 0;
        if (okvedKnown && candidate.Okved2.Contains(company.Okved2))
        {
            var refs = new[] { "E1", Add(EvidenceType.CompanyFeature, "okved2", company.Okved2) };
            reasons.Add(new RelevanceReasonDto("okved", $"Событие касается вашей отрасли: ОКВЭД {company.Okved2} — {Okved2Names.Find(company.Okved2) ?? company.IndustryName}.", [], refs));
            Feature(RankingFeatureCode.Okved, 1, options.OkvedWeight, true, refs);
        }
        else
            Feature(RankingFeatureCode.Okved, 0, options.OkvedWeight, okvedKnown, []);

        var sectorRows = taxonomy.Sensitivity.Where(row => row.Okved2 == company.Okved2).ToArray();
        var factorMatch = sectorRows.Where(row => candidate.Factors.Contains(row.Factor)).MaxBy(row => row.Weight);
        if (factorMatch is not null)
        {
            var refs = new[] { "E1", Add(EvidenceType.CompanyFeature, "okved2", company.Okved2) };
            reasons.Add(new RelevanceReasonDto("factor_" + factorMatch.Factor,
                $"Фактор «{FactorName(taxonomy, factorMatch.Factor)}» значим для отрасли ОКВЭД {company.Okved2} (вес {factorMatch.Weight.ToString("0.##", Russian)}): {factorMatch.Channel}", [], refs));
            Feature(RankingFeatureCode.Factor, factorMatch.Weight, options.FactorWeight, true, refs);
        }
        else
            Feature(RankingFeatureCode.Factor, 0, options.FactorWeight, sectorRows.Length > 0, []);

        var productMatches = taxonomy.ProductRules.Where(rule => company.Products.Contains(rule.Product) && candidate.Factors.Contains(rule.Factor))
            .GroupBy(rule => rule.Product).Select(group => group.First()).ToArray();
        if (productMatches.Length > 0)
        {
            var refs = new List<string> { "E1" };
            foreach (var rule in productMatches)
            {
                var reference = Add(EvidenceType.CompanyFeature, "products", rule.Product);
                refs.Add(reference);
                reasons.Add(new RelevanceReasonDto("product_" + rule.Product,
                    $"У компании есть продукт «{ProductNames.GetValueOrDefault(rule.Product, rule.Product)}» в Альфа-Банке, событие касается фактора «{FactorName(taxonomy, rule.Factor)}». {rule.Channel}", [], ["E1", reference]));
            }
            Feature(RankingFeatureCode.Product, 1, options.ProductWeight, true, refs);
        }
        else
            Feature(RankingFeatureCode.Product, 0, options.ProductWeight, true, []);

        var countries = company.FtsCountries.Where(candidate.Countries.Contains).ToArray();
        if (countries.Length > 0)
        {
            var refs = new[] { "E1", Add(EvidenceType.CompanyFeature, "fts_countries", countries) };
            reasons.Add(new RelevanceReasonDto("country",
                $"Событие касается страны {string.Join(", ", countries)}, которая указана в данных ФТС компании. Это не подтверждает объём или направление внешней торговли.", [], refs));
            Feature(RankingFeatureCode.Country, 1, options.CountryWeight, true, refs);
        }
        else
            Feature(RankingFeatureCode.Country, 0, options.CountryWeight, company.FtsCountries.Count > 0, []);

        var cpKnown = options.CpEnabled && (company.Incoming.Coverage > 0 || company.Outgoing.Coverage > 0);
        var cp = options.CpEnabled ? candidate.Okved2
            .SelectMany(code => new[] { (Direction: "incoming", Code: code, Exposure: company.Incoming), (Direction: "outgoing", Code: code, Exposure: company.Outgoing) })
            .Select(item => (item.Direction, item.Code, item.Exposure, Share: item.Exposure.SectorShares.GetValueOrDefault(item.Code)))
            .Where(item => item.Share > 0).OrderByDescending(item => item.Share).ThenBy(item => item.Direction, StringComparer.Ordinal)
            .Select(item => ((string Direction, string Code, PaymentExposure Exposure, decimal Share)?)item).FirstOrDefault() : null;
        if (cp is { } exposure)
        {
            var share = Math.Round(exposure.Share, 6);
            var refs = new[] { "E1", Add(EvidenceType.PaymentAggregate, $"{exposure.Direction}.sector_shares.{exposure.Code}",
                new { direction = exposure.Direction, okved2 = exposure.Code, share, coverage = Math.Round(exposure.Exposure.Coverage, 6) }) };
            reasons.Add(new RelevanceReasonDto("cp",
                $"{Percent(share)} наблюдаемого внешнего {(exposure.Direction == "incoming" ? "входящего" : "исходящего")} платёжного оборота приходится на компании отрасли ОКВЭД {exposure.Code}; покрытие профилями {Percent(exposure.Exposure.Coverage)}. Платежи не подтверждают поставки или тип отношений.", [], refs));
            Feature(RankingFeatureCode.Cp, share, options.CpWeight, true, refs);
        }
        else
            Feature(RankingFeatureCode.Cp, 0, options.CpWeight, cpKnown, []);

        var others = groupMembers.Where(member => member.CompanyId != company.CompanyId).ToArray();
        var groupMatch = others.Select(member =>
            {
                var weight = taxonomy.Sensitivity.Where(row => row.Okved2 == member.Okved2 && candidate.Factors.Contains(row.Factor))
                    .Select(row => (decimal?)row.Weight).Max() ?? 0;
                var product = taxonomy.ProductRules.Any(rule => member.Products.Contains(rule.Product) && candidate.Factors.Contains(rule.Factor));
                return (member.Okved2, Value: product ? 1 : weight);
            })
            .Where(item => item.Value > 0).OrderByDescending(item => item.Value).ThenBy(item => item.Okved2, StringComparer.Ordinal)
            .Select(item => ((string Okved2, decimal Value)?)item).FirstOrDefault();
        if (groupMatch is { } member)
        {
            var refs = new[] { "E1", Add(EvidenceType.GroupFeature, "group_member_okved2", member.Okved2) };
            reasons.Add(new RelevanceReasonDto("group",
                $"Событие затрагивает факторы, значимые для другой компании вашей группы (ОКВЭД {member.Okved2}).", [], refs));
            Feature(RankingFeatureCode.Group, member.Value, options.GroupWeight, true, refs);
        }
        else
            Feature(RankingFeatureCode.Group, 0, options.GroupWeight, others.Length > 0, []);

        var baseScore = Math.Round(contributions.Sum(item => item.Contribution), 6);
        var ageDays = Math.Max(0, (asOf - candidate.Source.PublishedAt).TotalDays);
        var recency = Math.Round((decimal)Math.Pow(2, -ageDays / 7), 6);
        var score = Math.Round(recency * candidate.SourceReliability * baseScore, 6);
        var eligible = contributions.Any(item => item.Value > 0) && baseScore >= options.MinBase && score >= options.MinScore
            && candidate.Source.PublishedAt >= WindowStart(asOf) && candidate.Source.PublishedAt <= asOf;
        return new RankedCandidate(candidate, baseScore, recency, score, eligible, contributions, reasons, evidence);
    }

    private static string FactorName(RankingTaxonomy taxonomy, string code) => taxonomy.FactorNames.GetValueOrDefault(code, code);

    private static string Percent(decimal value) => Math.Round(value * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
}