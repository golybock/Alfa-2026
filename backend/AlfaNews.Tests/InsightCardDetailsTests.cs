using System.Text.Json;
using System.Text.Json.Nodes;
using AlfaNews.Contracts.Domain;
using AlfaNews.Core.Access;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Insights;

namespace AlfaNews.Tests;

public sealed class InsightCardDetailsTests
{
    private static readonly PaymentExposure NoPayments = new(0, 0, 0, new Dictionary<string, decimal>());

    [Fact]
    public void SavedCardWithoutNewFieldsGetsDirectionAndPeriodOnRead()
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Examples", "insight.json")))!.AsObject();
        foreach (var name in new[] { "direction", "news_period_from", "news_period_to", "company_traits", "bank_data_as_of" })
            node.Remove(name);
        var card = AccessService.ReadReadyInsight(new InsightEntity { Status = "ready", BodyJson = JsonSerializer.SerializeToElement(node) });

        Assert.Equal(ConsequenceType.Risk, card.Direction);
        Assert.Equal(DateTimeOffset.Parse("2026-10-15T00:00:00+03:00"), card.NewsPeriodFrom);
        Assert.Equal(card.AsOf, card.NewsPeriodTo);
    }

    [Fact]
    public void TraitsFollowCompanyReasonsAndAddGroupRevenue()
    {
        var relevance = new RelevanceDto(0.5m, 1,
        [
            Reason("factor_key_rate", "Ключевая ставка"),
            Reason("okved", "Строительство зданий"),
            Reason("product_loan", "Ваш продукт: кредит"),
            Reason("cp", "Ваши поставщики")
        ], []);
        var traits = CompanyTraits.Build(relevance, Company("group_1"), new GroupProfile("group_1", ["U_1", "U_2"], 26_000_000_000m, 2, "v1"));

        Assert.Equal(["okved", "product_loan", "cp", "group"], traits.Select(trait => trait.Code));
        Assert.Equal(["Строительство зданий", "Кредит в банке", "Ваши поставщики", "Группа · 26,0 млрд ₽"], traits.Select(trait => trait.Label));
        Assert.Equal(26_000_000_000m, traits[^1].AmountRub);
    }

    [Fact]
    public void GroupWithoutKnownRevenueAndCompanyWithoutGroup()
    {
        var relevance = new RelevanceDto(0.5m, 1, [Reason("group", "Компания вашей группы")], []);
        Assert.Equal("Группа компаний", Assert.Single(CompanyTraits.Build(relevance, Company("group_1"), null)).Label);
        Assert.Empty(CompanyTraits.Build(new RelevanceDto(0.5m, 1, [], []), Company(null), null));
        Assert.Equal("540 млн ₽", CompanyTraits.Amount(540_400_000m));
    }

    [Fact]
    public void UnreviewedBankNoteIsShownOnlyWhenCardCitesBankResearch()
    {
        var evidence = new Dictionary<string, EvidenceDto>
        {
            ["E1"] = new(EvidenceType.News, "Новость", null, "Ставка 16%.", null, null, null, null, "news", null, null, null, false, null, null),
            ["E3"] = new(EvidenceType.BankResearch, "Обзор ставки", null, "Банк ожидает снижения.", new DateOnly(2026, 10, 1), "doc", 1, "c1",
                "bank_view", null, null, ReviewStatus.NotReviewed, false, null, null)
        };
        string[] tool = [AgentTools.UnreviewedBankText, "Страны ФТС по компании не указаны."];
        ClaimDto Claim(string id, string reference) => new(id, "Текст.", Origin.SourceFact, [reference], ClaimVerification.CodePassed, null);

        Assert.Equal(["Страны ФТС по компании не указаны."], InsightAgent.ShownLimitations(false, [Claim("C1", "E1")], evidence, tool));
        Assert.Equal(["Текст обзора банка не проверен вручную: Обзор ставки.", "Страны ФТС по компании не указаны."],
            InsightAgent.ShownLimitations(false, [Claim("C1", "E1"), Claim("C2", "E3")], evidence, tool));
    }

    private static RelevanceReasonDto Reason(string code, string label) => new(code, label, [], [], label);

    private static CompanyProfile Company(string? groupId) => new("U_1", groupId, "41.20", "41", "Строительство", null, null, null, null,
        null, ["loan"], [], NoPayments, NoPayments, "v1");
}