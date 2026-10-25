using System.Text.Json;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Insights;
using AlfaNews.Core.Llm;

namespace AlfaNews.Tests;

public sealed class LlmSalvageTests
{
    private static readonly IReadOnlyDictionary<string, EvidenceDto> Evidence = new Dictionary<string, EvidenceDto>
    {
        ["E1"] = new(EvidenceType.News, "Новость", null, "Банк России сохранил ставку 16%.", null, null, null, null,
            "news", null, "test", null,
            false, null, null)
    };

    [Fact]
    public void NewsFactWithWrongNumberIsDroppedAndBadRangeFallsBackToLine()
    {
        var request = NewsRequest("Ставка равна 14%. Прогноз сохранён.");
        var (errors, value, salvage) = Validate<NewsExtractionDraft>(request, News(
            new { text = "Ставка 14%", line = 1, start = 0, end = 99 },
            new { text = "Ставка 12%", line = 2, start = (int?)null, end = (int?)null }));

        Assert.Empty(errors);
        var fact = Assert.Single(value!.Facts);
        Assert.Equal(new FactReference("Ставка 14%", 1, null, null), fact);
        Assert.Equal(["fact:1:range_reset", "fact:2:number_not_in_quote"], salvage!.Dropped);
    }

    [Fact]
    public void NewsWithoutValidFactsStillGoesToRepair()
    {
        var request = NewsRequest("Ставка равна 14%.");
        var (errors, _, salvage) = Validate<NewsExtractionDraft>(request,
            News(new { text = "Ставка 12%", line = 1, start = (int?)null, end = (int?)null }));

        Assert.Equal(["news:number_not_in_quote"], errors);
        Assert.Null(salvage);
    }

    [Fact]
    public void ClaimWithUnsupportedNumberIsDroppedFromEverySection()
    {
        var (errors, value, salvage) = Validate<InsightDraft>(DraftRequest(), Draft(
            "Ставка сохранена → проверьте кредиты",
            ("C1", "Банк России сохранил ставку 16%."), ("C2", "Ставка выросла до 18%."),
            ("C3", "Проверьте условия кредитов.")));

        Assert.Empty(errors);
        Assert.Equal(["C1", "C3"], value!.Claims.Select(claim => claim.Id));
        Assert.Equal(["C1"], value.WhatHappened);
        Assert.Empty(value.WhyItMatters);
        Assert.Equal(["C3"], Assert.Single(value.Consequences).ClaimIds);
        Assert.Equal(["claim:C2:number_not_in_evidence"], salvage!.Dropped);
        Assert.Equal("число не найдено в данных — C2", LlmSalvage.Describe(salvage.Dropped));
    }

    [Fact]
    public void UnknownEvidenceIsDroppedPerClaim()
    {
        var draft = System.Text.Json.Nodes.JsonNode.Parse(Draft("Ставка сохранена",
            ("C1", "Банк России сохранил ставку 16%."),
            ("C2", "Спрос на кредиты снизится.")))!;
        draft["claims"]![1]!["refs"] = new System.Text.Json.Nodes.JsonArray("E9");
        var (errors, value, salvage) = Validate<InsightDraft>(DraftRequest(), draft.ToJsonString());

        Assert.Empty(errors);
        Assert.Equal(["C1"], value!.Claims.Select(claim => claim.Id));
        Assert.Equal(["claim:C2:unknown_evidence"], salvage!.Dropped);
    }

    [Fact]
    public void DanglingClaimReferenceIsDroppedFromSections()
    {
        var draft = System.Text.Json.Nodes.JsonNode.Parse(Draft("Ставка сохранена → проверьте кредиты",
            ("C1", "Банк России сохранил ставку 16%."),
            ("C2", "Спрос на кредиты снизится."), ("C3", "Проверьте условия кредитов.")))!;
        draft["why_it_matters"]!.AsArray().Add("C7");
        draft["consequences"]![0]!["claim_ids"]!.AsArray().Add("C8");
        var (errors, value, salvage) = Validate<InsightDraft>(DraftRequest(), draft.ToJsonString());

        Assert.Empty(errors);
        Assert.Equal(["C2"], value!.WhyItMatters);
        Assert.Equal(["C2", "C3"], Assert.Single(value.Consequences).ClaimIds);
        Assert.Equal(["C1", "C2", "C3"], value.Claims.Select(claim => claim.Id));
        Assert.Equal(["claim:C7:unknown_claim_id", "claim:C8:unknown_claim_id"], salvage!.Dropped);
    }

    [Fact]
    public void DanglingOnlyActionStillGoesToRepair()
    {
        var draft = System.Text.Json.Nodes.JsonNode.Parse(Draft("Ставка сохранена → проверьте кредиты",
            ("C1", "Банк России сохранил ставку 16%."),
            ("C2", "Спрос на кредиты снизится.")))!;
        draft["actions"] = new System.Text.Json.Nodes.JsonArray("C9");
        var (errors, _, salvage) = Validate<InsightDraft>(DraftRequest(), draft.ToJsonString());

        Assert.Contains("claims:unknown_claim_id", errors);
        Assert.Null(salvage);
    }

    [Theory]
    [InlineData("Ставка 18% → проверьте кредиты", "card:what_happened_required")]
    [InlineData("Ставка выросла до 18%", null)]
    public void CardLevelProblemsStillGoToRepair(string headline, string? cardError)
    {
        // Первый случай: убрать пришлось единственное утверждение «что произошло»; второй — заголовок держится на убранном числе.
        var draft = cardError is null
            ? Draft(headline, ("C1", "Банк России сохранил ставку 16%."), ("C2", "Ставка выросла до 18%."),
                ("C3", "Проверьте условия кредитов."))
            : Draft(headline, ("C1", "Ставка выросла до 18%."), ("C2", "Спрос на кредиты снизится."),
                ("C3", "Проверьте условия кредитов."));
        var (errors, _, salvage) = Validate<InsightDraft>(DraftRequest(), draft);

        Assert.Contains(cardError is null ? "claims:C2:number_not_in_evidence" : "claims:C1:number_not_in_evidence",
            errors);
        Assert.Null(salvage);
    }

    [Fact]
    public void DroppingTheLastActionGoesToRepair()
    {
        var (errors, _, salvage) = Validate<InsightDraft>(DraftRequest(), Draft("Ставка сохранена → проверьте кредиты",
            ("C1", "Банк России сохранил ставку 16%."), ("C2", "Спрос на кредиты снизится."),
            ("C3", "Проверьте кредиты под 18%.")));

        Assert.Contains("claims:C3:number_not_in_evidence", errors);
        Assert.Null(salvage);
    }

    private static (IReadOnlyList<string> Errors, T? Value, LlmSalvageResult? Salvage) Validate<T>(LlmRequest request,
        string content)
    {
        var prompt = new LlmPromptCatalog().Render(request, request.Task);
        var errors = LlmOutputValidator.Validate<T>(content, prompt, request, out var value, out var salvage);
        return (errors, value, salvage);
    }

    private static LlmRequest NewsRequest(string text) => LlmPromptTests.Request(LlmTask.NewsExtract) with
    {
        Values = LlmPromptTests.Values(new
        {
            INPUT_JSON = new { text },
            TAXONOMY_JSON = new
            {
                factors = Array.Empty<string>(),
                okved2 = Array.Empty<string>(),
                countries = Array.Empty<string>(),
                regions = Array.Empty<string>(),
                commodities = Array.Empty<string>()
            }
        })
    };

    private static string News(params object[] facts) => JsonSerializer.Serialize(new
    {
        headline = "Тест",
        event_date = (string?)null,
        facts,
        participants = Array.Empty<string>(),
        tags = new
        {
            factors = Array.Empty<string>(),
            okved2 = Array.Empty<string>(),
            countries = Array.Empty<string>(),
            regions = Array.Empty<string>(),
            commodities = Array.Empty<string>()
        },
        commentary = false
    });

    private static LlmRequest DraftRequest()
    {
        var request = LlmPromptTests.Request(LlmTask.Draft);
        var values = new Dictionary<string, JsonElement>(request.Values)
        {
            ["CONTEXT_JSON"] = JsonSerializer.SerializeToElement(new
                { evidence = new { E1 = new { quote = "Банк России сохранил ставку 16%." } } })
        };
        return request with { Values = values, ValidateOutput = json => InsightChecks.Validate(json, Evidence) };
    }

    // C1 — что произошло, C2 — почему важно, C3 — действие и последствие.
    private static string Draft(string headline, params (string Id, string Text)[] claims) => JsonSerializer.Serialize(
        new
        {
            headline,
            why_you = "Компания зависит от стоимости кредита.",
            what_happened = new[] { claims[0].Id },
            why_it_matters = new[] { claims[1].Id },
            consequences = claims.Length > 2
                ? new[] { new { type = "risk", claim_ids = new[] { claims[1].Id, claims[2].Id } } }
                : [],
            actions = claims.Skip(2).Select(claim => claim.Id).ToArray(),
            bank_view = new { status = "not_found", claim_ids = Array.Empty<string>() },
            claims = claims.Select(claim => new
            {
                id = claim.Id,
                text = claim.Text,
                origin = "source_fact",
                refs = new[] { "E1" },
                confidence = (string?)null
            }),
            limitations = Array.Empty<string>(),
            suggested_questions = Array.Empty<string>()
        });
}