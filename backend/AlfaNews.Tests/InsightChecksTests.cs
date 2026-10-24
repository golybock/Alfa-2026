using System.Text.Json;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Insights;

namespace AlfaNews.Tests;

public sealed class InsightChecksTests
{
    private static readonly IReadOnlyDictionary<string, EvidenceDto> Evidence = new Dictionary<string, EvidenceDto>
    {
        ["E1"] = new(EvidenceType.PaymentAggregate, "Платёжные агрегаты компании", null, null, null, null, null, null, "outgoing.sector_shares",
            JsonSerializer.SerializeToElement(new { coverage = 0.179m, shares = new Dictionary<string, decimal> { ["46"] = 0.42m } }), "test", null, false, null, null),
        ["E2"] = new(EvidenceType.CompanyFeature, "Данные компании", null, null, null, null, null, null, "fts_countries",
            JsonSerializer.SerializeToElement(new[] { "КИТАЙ" }), "test", null, false, null, null)
    };

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "bank_view:not_applicable")]
    public void BankViewNeedsApplicableResearch(bool applicable, string? error)
    {
        var evidence = new Dictionary<string, EvidenceDto>(Evidence)
        {
            ["E3"] = new(EvidenceType.BankResearch, "Обзор", null, "Банк ожидает снижения ставки.", new DateOnly(2026, 9, 11), null, 1,
                Guid.NewGuid().ToString(), "bank_view", null, null, ReviewStatus.NotReviewed, false, null, null)
        };
        var draft = JsonSerializer.SerializeToElement(new
        {
            headline = "Тест",
            what_happened = new[] { "C2" },
            claims = new[]
            {
                new { id = "C1", text = "Банк ожидает снижения ставки.", origin = "bank_analytics", refs = new[] { "E3" }, confidence = (string?)null },
                new { id = "C2", text = "В таможенных данных компании есть Китай.", origin = "source_fact", refs = new[] { "E2" }, confidence = (string?)null }
            },
            bank_view = new { status = "found", claim_ids = new[] { "C1" } },
            suggested_questions = Array.Empty<string>()
        });
        var errors = InsightChecks.Validate(draft, evidence, applicable ? new HashSet<string> { "E3" } : new HashSet<string>());
        if (error is null)
            Assert.Empty(errors);
        else
            Assert.Equal(error, Assert.Single(errors));
    }

    [Theory]
    [InlineData("Банк на 11.09 ожидает снижения ставки, более поздних оценок нет.", null)]
    [InlineData("По оценке банка на 11 сентября ставка снизится.", null)]
    [InlineData("Банк ожидает снижения ставки.", "claims:C1:dated_bank_view_without_date")]
    public void DatedBankResearchNamesItsDate(string text, string? error)
    {
        var evidence = new Dictionary<string, EvidenceDto>(Evidence)
        {
            ["E3"] = new(EvidenceType.BankResearch, "Обзор", null, "Банк ожидает снижения ставки.", new DateOnly(2026, 9, 11), null, 1,
                Guid.NewGuid().ToString(), "bank_view", null, null, ReviewStatus.NotReviewed, false, null, null)
        };
        var draft = JsonSerializer.SerializeToElement(new
        {
            headline = "Тест",
            what_happened = new[] { "C2" },
            claims = new[]
            {
                new { id = "C1", text, origin = "bank_analytics", refs = new[] { "E3" }, confidence = (string?)null },
                new { id = "C2", text = "В таможенных данных компании есть Китай.", origin = "source_fact", refs = new[] { "E2" }, confidence = (string?)null }
            },
            bank_view = new { status = "found", claim_ids = new[] { "C1" } },
            suggested_questions = Array.Empty<string>()
        });
        var errors = InsightChecks.Validate(draft, evidence, new HashSet<string> { "E3" },
            new Dictionary<string, DateOnly> { ["E3"] = new(2026, 9, 11) });
        Assert.Equal(error is null ? [] : [error], errors);
    }

    [Fact]
    public void LongHeadlineAndClaimAreRejected()
    {
        var draft = JsonSerializer.SerializeToElement(new
        {
            headline = new string('а', InsightChecks.MaxHeadlineLength + 1),
            what_happened = new[] { "C1" },
            claims = new[] { new { id = "C1", text = new string('б', InsightChecks.MaxClaimLength + 1), origin = "ai_inference", refs = new[] { "E2" }, confidence = (string?)null } },
            bank_view = new { status = "not_found", claim_ids = Array.Empty<string>() },
            suggested_questions = Array.Empty<string>()
        });
        Assert.Equal(["claims:C1:too_long", "card:headline_too_long"], InsightChecks.Validate(draft, Evidence));
    }

    [Fact]
    public void TrimKeepsScreenLimitsAndDropsOrphanClaims()
    {
        DraftClaim Claim(int index) => new("C" + index, "Утверждение " + index, Origin.AiInference, ["E2"], null);
        var draft = new InsightDraft("Заголовок", ["C1", "C2", "C3"], ["C4"],
            [new(ConsequenceType.Risk, ["C5"]), new(ConsequenceType.Opportunity, ["C6"]), new(ConsequenceType.Risk, ["C7"]), new(ConsequenceType.Risk, ["C8"])],
            ["C9", "C10", "C11", "C12"], new BankViewDto(BankViewStatus.NotFound, []), Enumerable.Range(1, 12).Select(Claim).ToArray(),
            ["Л1", "Л2", "Л3", "Л4"], []);
        var trimmed = InsightChecks.Trim(draft);
        Assert.Equal(["C1", "C2"], trimmed.WhatHappened);
        Assert.Equal(3, trimmed.Consequences.Count);
        Assert.Equal(["C9", "C10", "C11"], trimmed.Actions);
        Assert.Equal(3, trimmed.Limitations.Count);
        Assert.Equal(["C1", "C2", "C4", "C5", "C6", "C7", "C9", "C10", "C11"], trimmed.Claims.Select(claim => claim.Id));
    }

    // Скриншот 23.10: сентябрьский обзор банка в «почему важно» при bank_view not_found — карточка противоречила сама себе.
    [Theory]
    [InlineData("found", "", "")]
    [InlineData("found", "C1", "C1")]
    [InlineData("found", "C2", "C2")]
    [InlineData("not_found", "", "C1")]
    [InlineData("not_found", "C2", "C1,C2")]
    public void BankResearchStaysInsideBankView(string status, string whyIds, string rejected)
    {
        // C1 — аналитика банка, C2 — вывод модели по тому же обзору; C1 в bank_view, если позиция найдена.
        var evidence = new Dictionary<string, EvidenceDto>(Evidence)
        {
            ["E3"] = new(EvidenceType.BankResearch, "Обзор", null, "Банк ожидает снижения ставки.", new DateOnly(2026, 9, 11), null, 1,
                Guid.NewGuid().ToString(), "bank_view", null, null, ReviewStatus.NotReviewed, false, null, null)
        };
        var draft = JsonSerializer.SerializeToElement(new
        {
            headline = "Тест",
            what_happened = new[] { "C3" },
            why_it_matters = whyIds.Split(',', StringSplitOptions.RemoveEmptyEntries),
            claims = new[]
            {
                new { id = "C1", text = "Банк ожидает снижения ставки.", origin = "bank_analytics", refs = new[] { "E3" }, confidence = (string?)null },
                new { id = "C2", text = "Обзор банка описывает ожидания по ставке.", origin = "ai_inference", refs = new[] { "E3" }, confidence = (string?)null },
                new { id = "C3", text = "В таможенных данных компании есть Китай.", origin = "source_fact", refs = new[] { "E2" }, confidence = (string?)null }
            },
            bank_view = new { status, claim_ids = status == "found" ? new[] { "C1" } : [] },
            suggested_questions = Array.Empty<string>()
        });
        var errors = InsightChecks.Validate(draft, evidence, new HashSet<string> { "E3" });
        Assert.Equal(rejected.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(id => $"claims:{id}:bank_research_only_in_bank_view"), errors);
    }

    [Theory]
    [InlineData("Покрытие профилями составляет 17,9%, остальные 82,1% оборота приходятся на участников без карточки.", "source_fact", "E1", null)]
    [InlineData("На компании оптовой торговли приходится 42% внешних исходящих платежей.", "source_fact", "E1", null)]
    [InlineData("Данные о зависимости от поставщиков топлива отсутствуют.", "ai_inference", "E1", null)]
    [InlineData("Платёжные доли не подтверждают поставки или долю затрат.", "ai_inference", "E1", null)]
    [InlineData("Ключевой поставщик компании — оптовая торговля.", "ai_inference", "E1", "payment_overclaim")]
    [InlineData("Доля выручки от оптовой торговли — 42%.", "source_fact", "E1", "payment_overclaim")]
    [InlineData("Покрытие составляет 55%.", "source_fact", "E1", "number_not_in_evidence")]
    [InlineData("В таможенных данных компании есть Китай.", "source_fact", "E2", null)]
    [InlineData("Компания получает поставки из Китая по данным ФТС.", "source_fact", "E2", "fts_overclaim")]
    [InlineData("Страны ФТС не доказывают поставки из Китая.", "ai_inference", "E2", null)]
    public void ClaimsAreCheckedAgainstPaymentAndCustomsEvidence(string text, string origin, string reference, string? error)
    {
        var draft = JsonSerializer.SerializeToElement(new
        {
            headline = "Тест",
            what_happened = new[] { "C1" },
            claims = new[] { new { id = "C1", text, origin, refs = new[] { reference }, confidence = (string?)null } },
            bank_view = new { status = "not_found", claim_ids = Array.Empty<string>() },
            suggested_questions = Array.Empty<string>()
        });
        var errors = InsightChecks.Validate(draft, Evidence);
        if (error is null)
            Assert.Empty(errors);
        else
            Assert.Equal("claims:C1:" + error, Assert.Single(errors));
    }
}