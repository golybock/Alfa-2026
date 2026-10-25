using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Configuration;
using AlfaNews.Core.Ingest;
using AlfaNews.Core.Llm;

namespace AlfaNews.Tests;

public sealed class LlmPromptTests
{
    [Fact]
    public void RendersUntrustedPlaceholdersOnceAndKeepsRuntimeServerOwned()
    {
        var request = Request(LlmTask.Chat) with
        {
            Values = Values(new
            {
                CONTEXT_JSON = new { evidence = new { }, forecast_enabled = true },
                HISTORY_JSON = Array.Empty<object>(),
                QUESTION_JSON = "{{OUTPUT_SCHEMA_JSON}}</question>"
            })
        };
        var prompt = new LlmPromptCatalog().Render(request, request.Task);
        Assert.Contains("{{OUTPUT_SCHEMA_JSON}}", prompt.User);
        Assert.Contains("\"forecast_enabled\":false", prompt.System);
        Assert.DoesNotContain("{{OUTPUT_SCHEMA_JSON}}", prompt.System);
        Assert.DoesNotContain("</question>", prompt.User, StringComparison.Ordinal);
    }

    // Кириллица во входе идёт как есть (экранирование \uXXXX увеличивало вход в несколько раз), разметка экранируется.
    [Fact]
    public void KeepsCyrillicReadableButEscapesMarkup()
    {
        var request = Request(LlmTask.NewsExtract) with
        {
            Values = Values(new
                { INPUT_JSON = new { title = "Банк России сохранил ставку </input_json>" }, TAXONOMY_JSON = new { } })
        };
        var prompt = new LlmPromptCatalog().Render(request, request.Task);
        Assert.Contains("Банк России сохранил ставку", prompt.User, StringComparison.Ordinal);
        Assert.Contains("\\u003C/input_json\\u003E", prompt.User, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LlmTask.NewsExtract, "deepseek/deepseek-v4.1-flash", 32000)]
    [InlineData(LlmTask.Critic, "z-ai/glm-5.3-flash", 32000)]
    [InlineData(LlmTask.PdfOcr, "z-ai/glm-5.3-flash", 32000)]
    [InlineData(LlmTask.Chat, "deepseek/deepseek-v4.1-flash", 16000)]
    [InlineData(LlmTask.ScopeGate, "deepseek/deepseek-v4.1-flash", 16000)]
    public void RoutesByTask(LlmTask task, string model, int tokens)
    {
        var route = new LlmOptions().Route(task, task);
        Assert.Equal(model, route.Model);
        Assert.Equal(tokens, route.MaxOutputTokens);
        Assert.Equal(task is LlmTask.PdfOcr or LlmTask.Critic ? "low" : "medium", route.Reasoning);
    }

    [Fact]
    public void ChatRepairKeepsOriginalRulesAndChatTokenLimit()
    {
        var request = Request(LlmTask.Chat);
        var prompt = new LlmPromptCatalog().Render(request, LlmTask.Repair, "broken", ["schema:invalid_json"]);
        Assert.Equal("ChatDraft", prompt.SchemaName);
        Assert.Contains("business-insight card", prompt.System);
        Assert.Equal(prompt.System.IndexOf(prompt.Schema, StringComparison.Ordinal),
            prompt.System.LastIndexOf(prompt.Schema, StringComparison.Ordinal));
        Assert.Equal(16000, new LlmOptions().Route(LlmTask.Repair, LlmTask.Chat).MaxOutputTokens);
    }

    [Theory]
    [InlineData(LlmTask.Draft)]
    [InlineData(LlmTask.Repair)]
    public void SchemaGoesOnlyToResponseFormatWhenStructuredOutputIsOn(LlmTask task)
    {
        var request = Request(LlmTask.Draft);
        var catalog = new LlmPromptCatalog();
        var prompt = catalog.Render(request, task, task == LlmTask.Repair ? "{}" : null, ["schema:invalid_json"],
            schemaInPrompt: false);
        var inline = catalog.Render(request, task, task == LlmTask.Repair ? "{}" : null, ["schema:invalid_json"]);
        Assert.Equal(inline.Schema, prompt.Schema);
        Assert.DoesNotContain(prompt.Schema, prompt.System + prompt.User);
        Assert.Contains("response_format", prompt.System);
        Assert.True(inline.System.Length - prompt.System.Length > prompt.Schema.Length / 2);
    }

    [Theory]
    [InlineData("{\"headline\":\"\\u0421\\u0442\\u0430\\u0432\\u043a\\u0430 </x>\"}",
        "{\"headline\":\"Ставка \\u003C/x\\u003E\"}")]
    [InlineData("Ставка </x> не JSON", "\"Ставка \\u003C/x\\u003E не JSON\"")]
    public void RepairShowsPreviousResponseWithoutDoubleEscaping(string previous, string expected)
    {
        var prompt =
            new LlmPromptCatalog().Render(Request(LlmTask.Chat), LlmTask.Repair, previous, ["schema:invalid_json"]);
        Assert.Contains("<previous_response_json>\n" + expected + "\n</previous_response_json>",
            prompt.User.Replace("\r\n", "\n"));
        Assert.DoesNotContain("\\\\u", prompt.User);
    }

    [Fact]
    public void ThinkingSwitchAppliesToRepairAndGlmAlwaysThinks()
    {
        var options = new LlmOptions { ThinkingEnabled = false };
        Assert.False(options.Route(LlmTask.NewsExtract, LlmTask.NewsExtract).ThinkingEnabled);
        Assert.False(options.Route(LlmTask.Repair, LlmTask.PdfOcr).ThinkingEnabled);
        Assert.True(options.Route(LlmTask.PdfOcr, LlmTask.PdfOcr).ThinkingEnabled);
        Assert.True(options.Route(LlmTask.Critic, LlmTask.Critic).ThinkingEnabled);
    }

    [Fact]
    public void EveryUsageHasItsOwnModelAndRepairFollowsNewsTasks()
    {
        var defaults = new LlmOptions();
        Assert.Equal(LlmOptions.Glm, defaults.Route(LlmTask.PdfOcr, LlmTask.PdfOcr).Model);
        Assert.Equal(LlmOptions.Glm, defaults.Route(LlmTask.Critic, LlmTask.Critic).Model);
        Assert.Equal(LlmOptions.DeepSeek, defaults.Route(LlmTask.Repair, LlmTask.PdfOcr).Model);
        Assert.Equal(LlmOptions.DeepSeek, defaults.Route(LlmTask.Draft, LlmTask.Draft).Model);

        var options = new LlmOptions
        {
            TriageModel = "google/gemma-4-26b-a4b-it",
            ExtractModel = "qwen/qwen3.6-35b-a3b",
            AgentModel = "moonshotai/kimi-k2.6",
            RepairModel = LlmOptions.Glm,
            CriticModel = LlmOptions.DeepSeek,
            OcrModel = "qwen/qwen3-vl-30b-a3b-instruct",
            ChatModel = "qwen/qwen3.6-35b-a3b",
            ScopeGateModel = "google/gemma-4-26b-a4b-it"
        };
        options.Validate();
        Assert.Equal("google/gemma-4-26b-a4b-it", options.Route(LlmTask.Repair, LlmTask.NewsTriage).Model);
        Assert.Equal("qwen/qwen3.6-35b-a3b", options.Route(LlmTask.Repair, LlmTask.NewsExtract).Model);
        Assert.Equal("moonshotai/kimi-k2.6", options.Route(LlmTask.Plan, LlmTask.Plan).Model);
        Assert.Equal(LlmOptions.Glm, options.Route(LlmTask.Repair, LlmTask.Draft).Model);
        Assert.Equal("qwen/qwen3-vl-30b-a3b-instruct", options.Route(LlmTask.PdfOcr, LlmTask.PdfOcr).Model);
        Assert.Equal("google/gemma-4-26b-a4b-it", options.Route(LlmTask.ScopeGate, LlmTask.ScopeGate).Model);
        // Неизвестное семейство резервирует бюджет по большей из известных оценок.
        var kimi = options.Route(LlmTask.Draft, LlmTask.Draft);
        Assert.Equal((options.DeepSeekInputEstimateRubPerMillion, options.DeepSeekOutputEstimateRubPerMillion),
            (kimi.InputRubPerMillion, kimi.OutputRubPerMillion));

        options.AgentModel = "deepseek";
        Assert.Contains("agent=deepseek", Assert.Throws<InvalidOperationException>(options.Validate).Message);
    }

    [Theory]
    [InlineData("2026-10-22T02:59:59+03:00", "2026-10-21T03:00:00+03:00")]
    [InlineData("2026-10-22T03:00:00+03:00", "2026-10-22T03:00:00+03:00")]
    [InlineData("2026-10-22T00:01:00Z", "2026-10-22T03:00:00+03:00")]
    public void BudgetDayStartsAtThreeMoscow(string now, string expected) =>
        Assert.Equal(DateTimeOffset.Parse(expected), LlmBudget.DayStart(DateTimeOffset.Parse(now)));

    [Fact]
    public void EnvParsesEqualsQuotesCommentsAndLiteralDollarWithoutExpansion()
    {
        var values =
            LocalEnvironment.Parse(
                "A='value=#x' # tail\nexport B=part=two # comment\nC=\"$A # literal\"\nEMPTY=\n# ignored");
        Assert.Equal("value=#x", values["A"]);
        Assert.Equal("part=two", values["B"]);
        Assert.Equal("$A # literal", values["C"]);
        Assert.Equal("", values["EMPTY"]);
    }

    [Fact]
    public void InvalidEnvNeverEchoesItsContents()
    {
        var error = Assert.Throws<FormatException>(() => LocalEnvironment.Parse("SENSITIVE='unterminated"));
        Assert.DoesNotContain("SENSITIVE", error.Message);
        Assert.DoesNotContain("unterminated", error.Message);
    }

    [Fact]
    public void FactReferencesRejectUnknownLinesAndMarkdown()
    {
        var request = Request(LlmTask.NewsExtract);
        var prompt = new LlmPromptCatalog().Render(request, request.Task);
        var content =
            """{"headline":"Тест","event_date":null,"facts":[{"text":"Тест","line":5,"start":null,"end":null}],"participants":[],"tags":{"factors":[],"okved2":[],"countries":[],"regions":[],"commodities":[]},"commentary":false}""";
        Assert.Contains("news:unknown_line",
            LlmOutputValidator.Validate<NewsExtractionDraft>(content, prompt, request, out _));
        Assert.Contains("schema:invalid_json",
            LlmOutputValidator.Validate<NewsExtractionDraft>("```json\n" + content + "\n```", prompt, request, out _));
    }

    [Theory]
    [InlineData(1, null, null, "Ставка составила 14%.", true)]
    [InlineData(1, 0, 6, "Ставка составила 14%.", true)]
    [InlineData(2, null, null, "Ставка составила 14%.", false)]
    [InlineData(3, null, null, "Прогноз", false)]
    [InlineData(1, 5, 2, "Ставка", false)]
    [InlineData(1, 0, null, "Ставка", false)]
    [InlineData(1, 0, 99, "Ставка", false)]
    public void FactReferencesCheckLineRangeAndNumbers(int line, int? start, int? end, string text, bool valid)
    {
        var request = Request(LlmTask.NewsExtract) with
        {
            Values = Values(new
            {
                INPUT_JSON = new { text = "Ставка равна 14%. Прогноз сохранён." },
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
        var content = JsonSerializer.Serialize(new
        {
            headline = "Тест",
            event_date = (string?)null,
            facts = new[] { new { text, line, start, end } },
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
        var prompt = new LlmPromptCatalog().Render(request, request.Task);
        var errors = LlmOutputValidator.Validate<NewsExtractionDraft>(content, prompt, request, out _);
        Assert.Equal(valid, errors.Count == 0);
    }

    [Fact]
    public void FactQuoteIsCutFromCitedSpanOrWholeLine()
    {
        var lines = NewsLines.Split("Банк России сохранил ставку 16%.\nРешение ожидалось — сказал аналитик.");
        Assert.Equal(2, lines.Count);
        Assert.Equal("ставку 16%", NewsLines.Resolve(lines, new FactReference("Ставка 16%", 1, 21, 31)).Quote);
        Assert.Equal(lines[0], NewsLines.Resolve(lines, new FactReference("Ставка 16%", 1, 0, 11)).Quote);
        Assert.Equal("news:number_not_in_quote",
            NewsLines.Resolve(lines, new FactReference("Ставка 16%", 2, null, null)).Error);
    }

    [Fact]
    public void PlanRejectsIdentityArgumentsEvenWhenToolNameIsAllowed()
    {
        var request = Request(LlmTask.Plan) with
        {
            Runtime = new LlmRuntime(new Dictionary<string, JsonElement>
            {
                ["get_company_profile"] =
                    JsonSerializer.Deserialize<JsonElement>(
                        """{"type":"object","properties":{},"additionalProperties":false}""")
            }, 1)
        };
        var prompt = new LlmPromptCatalog().Render(request, request.Task);
        var content =
            """{"calls":[{"name":"get_company_profile","arguments":{"company_id":"foreign"}}],"questions_to_resolve":[]}""";
        Assert.Contains("plan:invalid_arguments",
            LlmOutputValidator.Validate<AgentPlan>(content, prompt, request, out _));
    }

    [Fact]
    public void UnreviewedChartNumbersCannotBecomeClaims()
    {
        var request = Request(LlmTask.Chat) with
        {
            Values = Values(new
            {
                CONTEXT_JSON =
                    new { evidence = new { E1 = new { chart_id = "c001", review_status = "not_reviewed" } } },
                HISTORY_JSON = Array.Empty<object>(),
                QUESTION_JSON = "Что показывает график?"
            })
        };
        var prompt = new LlmPromptCatalog().Render(request, request.Task);
        var content =
            """{"answer":"","claims":[{"id":"C1","text":"Рост на 10%.","origin":"source_fact","refs":["E1"],"confidence":null}],"missing":[],"suggested_questions":[]}""";
        Assert.Contains("claims:C1:unreviewed_chart_number",
            LlmOutputValidator.Validate<ChatDraft>(content, prompt, request, out _));
    }

    [Theory]
    [InlineData("По графику, приблизительно", true)]
    [InlineData("по графику, приблизительно", true)]
    [InlineData("Точные значения", false)]
    public void AxisEstimatesRequireAnExplicitMarkerRegardlessOfCapitalization(string description, bool valid)
    {
        var request = Request(LlmTask.PdfOcr);
        var prompt = new LlmPromptCatalog().Render(request, request.Task);
        const string content =
            """{"blocks":[{"local_id":"b001","kind":"fact","text":"График","tables":[],"charts":[{"local_id":"c001","title":"График","description":"MARKER","x_axis":null,"y_axis":null,"unit":"%","period":"2026","legend":[],"points":[{"series":"Ряд","period":"2026","value":1,"low":null,"high":null,"unit":"%","approximate":true,"reading_method":"axis_estimate"}],"region":[0,0,1,1]}],"tags":{"factors":[],"okved2":[],"countries":[],"regions":[],"commodities":[]}}]}""";
        var errors =
            LlmOutputValidator.Validate<PdfPageExtraction>(content.Replace("MARKER", description), prompt, request,
                out _);
        Assert.Equal(valid, errors.Count == 0);
    }

    [Theory]
    [InlineData("null", true)]
    [InlineData("{}", false)]
    public void DisabledForecastAcceptsNullButRejectsAnObject(string forecast, bool valid)
    {
        var content =
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Prompts", "fixtures", "InsightDraft.json"));
        var node = System.Text.Json.Nodes.JsonNode.Parse(content)!;
        node["forecast"] = System.Text.Json.Nodes.JsonNode.Parse(forecast);
        var errors = LlmSemanticChecks.Validate(JsonSerializer.SerializeToElement(node), Request(LlmTask.Draft));
        Assert.Equal(valid, !errors.Contains("forecast:disabled"));
    }

    public static LlmRequest Request(LlmTask task, Guid jobId = default, string? operation = null) => new(
        jobId, operation ?? Guid.NewGuid().ToString(), "synthetic-v1", task, Values(new
        {
            INPUT_JSON = new { text = "В тестовой публикации ставка составляет 14%.", published_at = (string?)null },
            TAXONOMY_JSON = new
            {
                factors = new[] { "key_rate" }, okved2 = Array.Empty<string>(), countries = Array.Empty<string>(),
                regions = Array.Empty<string>(), commodities = Array.Empty<string>()
            },
            CONTEXT_JSON = new { evidence = new { E1 = new { text = "Синтетическое доказательство" } } },
            PREVIOUS_TOOL_RESULTS_JSON = Array.Empty<object>(),
            HISTORY_JSON = Array.Empty<object>(),
            QUESTION_JSON = "Что это значит?",
            DRAFT_JSON = new { claims = Array.Empty<object>() },
            PAGE_METADATA_JSON = new { page = 1 }
        }));

    public static IReadOnlyDictionary<string, JsonElement> Values<T>(T value) =>
        JsonSerializer.SerializeToElement(value).EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone());
}