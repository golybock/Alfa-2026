using System.Net;
using System.Text;
using System.Text.Json;
using AlfaNews.Core.Llm;

namespace AlfaNews.Tests;

public sealed class LlmClientTests
{
    [Theory]
    [InlineData("low")]
    [InlineData("high")]
    public async Task SendsTextCriticWithConfiguredEffortAndReadsRubleCost(string effort)
    {
        using var http = new HttpClient(new TestTransport(async request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            var body = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync());
            Assert.Equal(effort, body.GetProperty("reasoning_effort").GetString());
            Assert.Equal(32000, body.GetProperty("max_tokens").GetInt32());
            Assert.False(body.TryGetProperty("tools", out _));
            return Reply(
                """{"choices":[{"finish_reason":"stop","message":{"content":"{\"issues\":[]}"}}],"usage":{"prompt_tokens":42,"completion_tokens":96,"completion_tokens_details":{"reasoning_tokens":90},"cost":0.002}}""");
        }));
        var result = await new OpenAiApiClient(http, Options()).SendAsync(Prompt(),
            new LlmRoute("z-ai/glm-5.3-flash", effort, 32000, 1, 100, 100), null, CancellationToken.None);
        Assert.Null(result.Error);
        Assert.Equal(0.002m, result.ActualCostRub);
        Assert.Equal(90, result.ReasoningTokens);
    }

    [Theory]
    [InlineData(401, "llm_key_required", false)]
    [InlineData(400, "llm_request_rejected", false)]
    [InlineData(429, "llm_rate_limited", true)]
    [InlineData(503, "llm_unavailable", true)]
    public async Task ClassifiesHttpFailuresWithoutReturningProviderText(int status, string code, bool transient)
    {
        using var http =
            new HttpClient(new TestTransport(_ => Task.FromResult(Reply("private provider error", status))));
        var result =
            await new OpenAiApiClient(http, Options()).SendAsync(Prompt(), Route(), null, CancellationToken.None);
        Assert.Equal(code, result.Error);
        Assert.Equal(transient, result.Transient);
        Assert.Null(result.ActualCostRub);
    }

    [Theory]
    [InlineData(400,
        """{"error":{"message":"Budget has been exceeded! Current cost: 500.1, Max budget: 500.0","type":"budget_exceeded"}}""")]
    [InlineData(429, "ExceededBudget: budget exceeded for key")]
    public async Task GatewayBudgetRejectionIsBudgetExhaustedNotRetried(int status, string body)
    {
        using var http = new HttpClient(new TestTransport(_ => Task.FromResult(Reply(body, status))));
        var result =
            await new OpenAiApiClient(http, Options()).SendAsync(Prompt(), Route(), null, CancellationToken.None);
        Assert.Equal("budget_exhausted", result.Error);
        Assert.False(result.Transient);
    }

    [Fact]
    public async Task ReadsSharedKeySpendFromGatewayRootAndCachesIt()
    {
        var requests = new List<string>();
        using var http = new HttpClient(new TestTransport(request =>
        {
            requests.Add(request.Method + " " + request.RequestUri);
            Assert.Equal("synthetic", request.Headers.Authorization!.Parameter);
            return Task.FromResult(Reply(
                """{"key":"hash","info":{"spend":235.5,"total_spend":816.0,"max_budget":500.0,"budget_duration":"1d"}}"""));
        }));
        using var client = new OpenAiApiClient(http, Options());

        var first = await client.KeySpendAsync(CancellationToken.None);
        var second = await client.KeySpendAsync(CancellationToken.None);

        Assert.Equal(235.5m, first!.Spend);
        Assert.Equal(500m, first.MaxBudget);
        Assert.Same(first, second);
        Assert.Equal(["GET https://example.org/key/info"], requests);
    }

    [Theory]
    [InlineData(404, """{"detail":"Not Found"}""")]
    [InlineData(200, """{"info":{"spend":10.0,"max_budget":null}}""")]
    public async Task MissingKeyBudgetFallsBackToLocalLimit(int status, string body)
    {
        using var http = new HttpClient(new TestTransport(_ => Task.FromResult(Reply(body, status))));
        using var client = new OpenAiApiClient(http, Options());
        Assert.Null(await client.KeySpendAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PreservesUnknownCostAndHeaderCostOnMalformedEnvelope()
    {
        using var http = new HttpClient(new TestTransport(_ =>
        {
            var reply = Reply("malformed");
            reply.Headers.Add("x-litellm-response-cost", "0.125");
            return Task.FromResult(reply);
        }));
        var result =
            await new OpenAiApiClient(http, Options()).SendAsync(Prompt(), Route(), null, CancellationToken.None);
        Assert.Equal("invalid_envelope", result.Error);
        Assert.Equal(0.125m, result.ActualCostRub);
    }

    [Fact]
    public async Task LengthIsTerminalEvenWhenContentIsValidJson()
    {
        using var http = new HttpClient(new TestTransport(_ =>
            Task.FromResult(Reply("""{"choices":[{"finish_reason":"length","message":{"content":"{}"}}]}"""))));
        var result =
            await new OpenAiApiClient(http, Options()).SendAsync(Prompt(), Route(), null, CancellationToken.None);
        Assert.Equal("output_length", result.Error);
        Assert.False(result.Transient);
        Assert.Null(result.ActualCostRub);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AttachesOnePageAndUsesStructuredSchemaOnlyWhenConfigured(bool structuredOutput)
    {
        using var http = new HttpClient(new TestTransport(async request =>
        {
            var body = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync());
            var parts = body.GetProperty("messages")[1].GetProperty("content");
            Assert.Equal(2, parts.GetArrayLength());
            Assert.StartsWith("data:image/png;base64,",
                parts[1].GetProperty("image_url").GetProperty("url").GetString());
            Assert.Equal(structuredOutput, body.TryGetProperty("response_format", out var format));
            if (structuredOutput)
            {
                Assert.Equal("json_schema", format.GetProperty("type").GetString());
                var schema = format.GetProperty("json_schema");
                Assert.True(schema.GetProperty("strict").GetBoolean());
                Assert.Equal("Test", schema.GetProperty("name").GetString());
                Assert.Equal("{}", schema.GetProperty("schema").GetRawText());
            }

            return Reply("""{"choices":[{"finish_reason":"stop","message":{"content":"{}"}}]}""");
        }));
        var options = Options();
        options.StructuredOutput = structuredOutput;
        await new OpenAiApiClient(http, options).SendAsync(Prompt(), Route(), new LlmImage([1, 2, 3]),
            CancellationToken.None);
    }

    [Fact]
    public async Task DisablingThinkingOmitsEffortAndExplicitlyDisablesBothProviderFormats()
    {
        using var http = new HttpClient(new TestTransport(async request =>
        {
            var body = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync());
            Assert.Equal("disabled", body.GetProperty("thinking").GetProperty("type").GetString());
            Assert.False(body.GetProperty("reasoning").GetProperty("enabled").GetBoolean());
            Assert.False(body.GetProperty("reasoning").TryGetProperty("effort", out _));
            Assert.False(body.TryGetProperty("reasoning_effort", out _));
            return Reply("""{"choices":[{"finish_reason":"stop","message":{"content":"{}"}}]}""");
        }));
        await new OpenAiApiClient(http, Options()).SendAsync(Prompt(), Route() with
        {
            ThinkingEnabled = false
        }, null, CancellationToken.None);
    }

    private static LlmOptions Options() => new() { BaseUrl = "https://example.org/v1", ApiKey = "synthetic" };
    private static LlmPrompt Prompt() => new("system", "user", "{}", "Test", "v1", "hash");
    private static LlmRoute Route() => new("deepseek/deepseek-v4.1-flash", "medium", 8000, 1, 500, 500);

    private static HttpResponseMessage Reply(string content, int status = 200) => new((HttpStatusCode)status)
        { Content = new StringContent(content, Encoding.UTF8, "application/json") };

    private sealed class TestTransport(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request);
    }
}