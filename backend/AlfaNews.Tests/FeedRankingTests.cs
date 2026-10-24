using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Core.Feed;

namespace AlfaNews.Tests;

public sealed class FeedRankingTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 10, 23, 12, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public void OfficialRateNewsWithLoanAndMatrixWeightGivesThirtyNineHundredths()
    {
        var ranked = FeedRanking.Rank(Candidate(factors: ["key_rate"]), Company(products: ["loan"]), [], Taxonomy(), new RankingOptions(), AsOf);
        Assert.Equal(0.39m, ranked.Base);
        Assert.Equal(0.39m, ranked.Score);
        Assert.True(ranked.Eligible);
        Assert.Equal(Enum.GetValues<RankingFeatureCode>(), ranked.Contributions.Select(item => item.Code));
        Assert.Contains(ranked.Reasons, reason => reason.Code == "product_loan");
        Assert.All(ranked.Reasons, reason => Assert.All(reason.EvidenceRefs, reference => Assert.True(ranked.Evidence.ContainsKey(reference))));
        Assert.All(ranked.Reasons, reason => Assert.InRange(reason.Label?.Length ?? 0, 1, FeedRanking.LabelMaxLength));
        Assert.Equal("Ваш продукт: кредит", ranked.Reasons.Single(reason => reason.Code == "product_loan").Label);
        Assert.Equal(Significance.Low, FeedRanking.SignificanceOf(ranked.Base));
    }

    [Fact]
    public void CommentaryWithoutDecisionWeighsHalfOfTheSectorFactor()
    {
        var decision = FeedRanking.Rank(Candidate(factors: ["key_rate"]), Company(products: ["loan"]), [], Taxonomy(), new RankingOptions(), AsOf);
        var remark = FeedRanking.Rank(Candidate(factors: ["key_rate"]) with { Commentary = true }, Company(products: ["loan"]), [], Taxonomy(),
            new RankingOptions(), AsOf);
        var factor = decision.Contributions.Single(item => item.Code == RankingFeatureCode.Factor).Contribution;
        Assert.Equal(decision.Base - factor / 2, remark.Base);
        Assert.Contains("комментарий", remark.Reasons.Single(reason => reason.Code.StartsWith("factor_", StringComparison.Ordinal)).Text);
    }

    [Theory]
    [InlineData(0.35, Significance.Low)]
    [InlineData(0.40, Significance.Medium)]
    [InlineData(0.4999, Significance.Medium)]
    [InlineData(0.50, Significance.High)]
    public void SignificanceBandsFollowBase(double baseScore, Significance expected) =>
        Assert.Equal(expected, FeedRanking.SignificanceOf((decimal)baseScore));

    [Fact]
    public void ProductWithoutSectorFactorStaysBelowThresholdAndUnknownFeaturesStayUnknown()
    {
        var ranked = FeedRanking.Rank(Candidate(factors: ["key_rate"]), Company(okved2: "99", products: ["loan"]), [], Taxonomy(), new RankingOptions(), AsOf);
        Assert.Equal(0.15m, ranked.Base);
        Assert.Equal(0.15m, ranked.Score);
        Assert.False(ranked.Eligible);
        var strongFactor = FeedRanking.Rank(Candidate(factors: ["timber"]), Company(okved2: "16", products: []), [], Taxonomy(), new RankingOptions(), AsOf);
        Assert.Equal(0.30m, strongFactor.Base);
        Assert.True(strongFactor.Eligible);
        Assert.False(ranked.Contributions.Single(item => item.Code == RankingFeatureCode.Factor).Known);
        Assert.False(ranked.Contributions.Single(item => item.Code == RankingFeatureCode.Country).Known);
        Assert.False(ranked.Contributions.Single(item => item.Code == RankingFeatureCode.Group).Known);
    }

    [Fact]
    public void RecencyReliabilityAndThresholdsApplyWithoutFillingTheFeed()
    {
        var older = FeedRanking.Rank(Candidate(factors: ["key_rate"], age: TimeSpan.FromDays(3.5), reliability: 0.9m),
            Company(okved2: "99", products: ["loan"]), [], Taxonomy(), new RankingOptions(), AsOf);
        Assert.Equal(0.707107m, older.Recency);
        Assert.Equal(0.095459m, older.Score);
        Assert.False(older.Eligible);
        var unrelated = FeedRanking.Rank(Candidate(factors: ["timber"]), Company(products: ["loan"]), [], Taxonomy(), new RankingOptions(), AsOf);
        Assert.Equal(0m, unrelated.Base);
        Assert.False(unrelated.Eligible);
        Assert.Empty(unrelated.Reasons);
    }

    [Fact]
    public void WindowStartsAtMoscowMidnightSixDaysBack()
    {
        Assert.Equal(new DateTimeOffset(2026, 10, 17, 0, 0, 0, TimeSpan.FromHours(3)), FeedRanking.WindowStart(AsOf));
        var outside = FeedRanking.Rank(Candidate(factors: ["key_rate"], age: TimeSpan.FromDays(6.6)), Company(products: ["loan"]), [],
            Taxonomy(), new RankingOptions(), AsOf);
        Assert.False(outside.Eligible);
        var future = FeedRanking.Rank(Candidate(factors: ["key_rate"], age: TimeSpan.FromHours(-1)), Company(products: ["loan"]), [],
            Taxonomy(), new RankingOptions(), AsOf);
        Assert.False(future.Eligible);
    }

    [Fact]
    public void PaymentExposureTakesTheStrongerDirectionWithoutAdding()
    {
        var company = Company(products: [], incoming: Exposure(0.30m, ("46", 0.10m)), outgoing: Exposure(0.60m, ("46", 0.24m)));
        var ranked = FeedRanking.Rank(Candidate(factors: [], okved2: ["46"]), company, [], Taxonomy(), new RankingOptions(), AsOf);
        var cp = ranked.Contributions.Single(item => item.Code == RankingFeatureCode.Cp);
        Assert.Equal(0.24m, cp.Value);
        Assert.Contains("исходящего", ranked.Reasons.Single(reason => reason.Code == "cp").Text);
        Assert.Contains("покрытие профилями 60%", ranked.Reasons.Single(reason => reason.Code == "cp").Text);
        var disabled = FeedRanking.Rank(Candidate(factors: [], okved2: ["46"]), company, [], Taxonomy(), new RankingOptions { CpEnabled = false }, AsOf);
        Assert.Equal(0m, disabled.Contributions.Single(item => item.Code == RankingFeatureCode.Cp).Value);
    }

    [Fact]
    public void GroupMemberSensitivityCountsWithoutRevealingTheMember()
    {
        var member = Company(okved2: "41", products: []) with { CompanyId = "member" };
        var ranked = FeedRanking.Rank(Candidate(factors: ["key_rate"]), Company(okved2: "99", products: []), [member], Taxonomy(), new RankingOptions(), AsOf);
        var group = ranked.Contributions.Single(item => item.Code == RankingFeatureCode.Group);
        Assert.Equal(0.8m, group.Value);
        Assert.DoesNotContain("member", ranked.Reasons.Single(reason => reason.Code == "group").Text);
    }

    [Fact]
    public void SelectionOrdersByScoreThenDateThenIdAndKeepsAtMostFive()
    {
        var items = Enumerable.Range(0, 7).Select(index => FeedRanking.Rank(Candidate(factors: ["key_rate"], age: TimeSpan.FromHours(index)),
            Company(products: ["loan"]), [], Taxonomy(), new RankingOptions(), AsOf)).ToArray();
        var selected = FeedRanking.Select(items.Reverse(), 5);
        Assert.Equal(5, selected.Count);
        Assert.Equal(items.Take(5).Select(item => item.Candidate.EventId), selected.Select(item => item.Candidate.EventId));
    }

    [Fact]
    public void SameStoryTakesOnePlaceAndNextDifferentEventMovesUp()
    {
        // Векторы с заданным косинусом к оси x: 0,95 — пересказ, 0,88 — близкий текст; другая тема ортогональна всем.
        static float[] Vector(double cosine) => [(float)cosine, (float)Math.Sqrt(1 - cosine * cosine), 0];
        var company = Company(products: ["loan"]);
        var options = new RankingOptions();
        RankedCandidate Item(double cosine, int hours, string factor = "key_rate") => FeedRanking.Rank(
            Candidate(factors: [factor], age: TimeSpan.FromHours(hours)) with { Embedding = Vector(cosine) }, company, [], Taxonomy(), options, AsOf);
        var rate = Item(1, 0);
        var retold = Item(0.95, 1);
        var sameTagClose = Item(0.88, 2);
        var otherTagClose = Item(0.88, 3, "timber");
        var sameTagLater = Item(0.88, 30);
        var different = FeedRanking.Rank(Candidate(factors: ["key_rate"], age: TimeSpan.FromHours(4)) with { Embedding = [0, 0, 1] },
            company, [], Taxonomy(), options, AsOf);
        var unknown = FeedRanking.Rank(Candidate(factors: ["key_rate"], age: TimeSpan.FromHours(5)), company, [], Taxonomy(), options, AsOf);
        var rule = options.SameStory();
        Assert.True(rule.Matches(rate.Candidate, retold.Candidate));
        Assert.True(rule.Matches(rate.Candidate, sameTagClose.Candidate));
        Assert.False(rule.Matches(rate.Candidate, otherTagClose.Candidate));
        Assert.False(rule.Matches(rate.Candidate, sameTagLater.Candidate));
        Assert.False(rule.Matches(rate.Candidate, unknown.Candidate));
        var pool = new[] { unknown, different, sameTagLater, sameTagClose, retold, rate };
        Assert.Equal([rate.Candidate.EventId, different.Candidate.EventId, unknown.Candidate.EventId],
            FeedRanking.Select(pool, 3, rule).Select(item => item.Candidate.EventId));
        Assert.Equal(5, FeedRanking.Select(pool, 5).Count);

        // Транзитивность: B похоже на A, C — на B, но не на A; C всё равно пропускается.
        var bridge = Item(0.95, 1);
        // Поворот на двойной угол пересказа: косинус к A = 0,95² − sin², к B = 0,95.
        var sine = Math.Sqrt(1 - 0.95 * 0.95);
        float[] twice = [(float)(0.95 * 0.95 - sine * sine), (float)(2 * 0.95 * sine), 0];
        var chained = FeedRanking.Rank(Candidate(factors: ["key_rate"], age: TimeSpan.FromHours(2)) with { Embedding = twice },
            company, [], Taxonomy(), options, AsOf);
        Assert.False(new SameStoryRule(0.93, 1.01, TimeSpan.Zero).Matches(rate.Candidate, chained.Candidate));
        Assert.Equal([rate.Candidate.EventId],
            FeedRanking.Select([rate, bridge, chained], 5, new SameStoryRule(0.93, 1.01, TimeSpan.Zero)).Select(item => item.Candidate.EventId));
    }

    private static FeedCandidate Candidate(string[] factors, string[]? okved2 = null, TimeSpan? age = null, decimal reliability = 1m) => new(
        Guid.NewGuid(), "Банк России сохранил ключевую ставку", "Ставка сохранена.",
        new SourceSummaryDto("Банк России", "https://www.cbr.ru/press/", AsOf - (age ?? TimeSpan.Zero)), "Ставка сохранена.",
        reliability, "v1", factors.ToHashSet(), (okved2 ?? []).ToHashSet(), new HashSet<string>());

    private static CompanyProfile Company(string okved2 = "41", string[]? products = null, PaymentExposure? incoming = null,
        PaymentExposure? outgoing = null) => new("company", null, okved2 + ".20", okved2, "Отрасль", null, null, null, null, null,
        products ?? [], [], incoming ?? Exposure(0m), outgoing ?? Exposure(0m), "test");

    private static PaymentExposure Exposure(decimal coverage, params (string Code, decimal Share)[] shares) =>
        new(100m, coverage * 100m, coverage, shares.ToDictionary(item => item.Code, item => item.Share));

    private static RankingTaxonomy Taxonomy() => new(
        new Dictionary<string, string> { ["key_rate"] = "Ключевая ставка", ["timber"] = "Лес" },
        [new SensitivityRule("41", "key_rate", 0.8m, "Проектное финансирование."), new SensitivityRule("16", "timber", 1m, "Цены на лесопродукцию.")],
        [new ProductRule("loan", "key_rate", "Ставка кредита.")], "test");
}