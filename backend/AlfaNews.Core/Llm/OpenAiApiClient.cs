using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AlfaNews.Core.Llm;

public sealed class OpenAiApiClient(HttpClient http, LlmOptions options) : ILlmClient, IDisposable
{
    private static readonly TimeSpan KeySpendTtl = TimeSpan.FromSeconds(30);
    private readonly SemaphoreSlim keyGate = new(1, 1);
    private LlmKeySpend? keySpend;
    private DateTimeOffset keyCheckedAt = DateTimeOffset.MinValue;

    public async Task<LlmClientResponse> SendAsync(LlmPrompt prompt, LlmRoute route, LlmImage? image,
        CancellationToken cancellationToken)
    {
        JsonNode content = JsonValue.Create(prompt.User)!;
        if (image is not null)
            content = new JsonArray(
                new JsonObject { ["type"] = "text", ["text"] = prompt.User },
                new JsonObject
                {
                    ["type"] = "image_url",
                    ["image_url"] = new JsonObject
                        { ["url"] = $"data:{image.MediaType};base64,{Convert.ToBase64String(image.Bytes)}" }
                });
        var body = new JsonObject
        {
            ["model"] = route.Model,
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = prompt.System },
                new JsonObject { ["role"] = "user", ["content"] = content }),
            ["max_tokens"] = route.MaxOutputTokens,
            ["reasoning"] = route.ThinkingEnabled
                ? new JsonObject { ["enabled"] = true, ["effort"] = route.Reasoning }
                : new JsonObject { ["enabled"] = false },
            ["thinking"] = new JsonObject { ["type"] = route.ThinkingEnabled ? "enabled" : "disabled" }
        };
        if (route.ThinkingEnabled)
            body["reasoning_effort"] = route.Reasoning;
        if (options.StructuredOutput)
            body["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject
                {
                    ["name"] = prompt.SchemaName,
                    ["strict"] = true,
                    ["schema"] = JsonNode.Parse(prompt.Schema)
                }
            };
        using var request = new HttpRequestMessage(HttpMethod.Post, options.BaseUrl.TrimEnd('/') + "/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(route.TimeoutSeconds));
        decimal? headerCost = null;
        int? status = null;
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            status = (int)response.StatusCode;
            if (response.Headers.TryGetValues("x-litellm-response-cost", out var costs)
                && decimal.TryParse(costs.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture,
                    out var cost)
                && cost >= 0)
                headerCost = cost;
            await response.Content.LoadIntoBufferAsync(4 * 1024 * 1024, timeout.Token);
            var raw = await response.Content.ReadAsStringAsync(timeout.Token);
            JsonElement envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<JsonElement>(raw);
            }
            catch (JsonException)
            {
                return new LlmClientResponse(
                    Error: response.IsSuccessStatusCode ? "invalid_envelope" : Error(status.Value, raw),
                    Transient: !BudgetExceeded(raw) && (status == 429 || status >= 500), ActualCostRub: headerCost,
                    HttpStatus: status);
            }

            var usage = Property(envelope, "usage");
            var actualCost = Decimal(usage, "cost") ?? headerCost;
            var inputTokens = Long(usage, "prompt_tokens") ?? 0;
            var outputTokens = Long(usage, "completion_tokens") ?? 0;
            var reasoningTokens = Long(Property(usage, "completion_tokens_details"), "reasoning_tokens");
            if (!response.IsSuccessStatusCode)
                return new LlmClientResponse(Error: Error(status.Value, raw),
                    Transient: !BudgetExceeded(raw) && (status == 429 || status >= 500),
                    InputTokens: inputTokens, OutputTokens: outputTokens, ReasoningTokens: reasoningTokens,
                    ActualCostRub: actualCost, HttpStatus: status);
            var choices = Property(envelope, "choices");
            var choice = choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0
                ? choices[0]
                : default;
            var finish = String(choice, "finish_reason");
            var text = String(Property(choice, "message"), "content");
            var error = finish == "length" ? "output_length"
                : finish != "stop" ? "invalid_finish_reason"
                : string.IsNullOrWhiteSpace(text) ? "empty_response" : null;
            return new LlmClientResponse(text, error, InputTokens: inputTokens, OutputTokens: outputTokens,
                ReasoningTokens: reasoningTokens, ActualCostRub: actualCost, HttpStatus: status, FinishReason: finish);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new LlmClientResponse(Error: "llm_timeout", Transient: true, ActualCostRub: headerCost,
                HttpStatus: status);
        }
        catch (HttpRequestException)
        {
            return new LlmClientResponse(Error: "llm_transport", Transient: true, ActualCostRub: headerCost,
                HttpStatus: status);
        }
    }

    public async Task<LlmKeySpend?> KeySpendAsync(CancellationToken cancellationToken)
    {
        await keyGate.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (now - keyCheckedAt >= KeySpendTtl)
            {
                keyCheckedAt = now;
                keySpend = await ReadKeySpendAsync(now, cancellationToken);
            }

            return keySpend;
        }
        finally
        {
            keyGate.Release();
        }
    }

    private async Task<LlmKeySpend?> ReadKeySpendAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var root = options.BaseUrl.TrimEnd('/');
        if (root.EndsWith("/v1", StringComparison.Ordinal))
            root = root[..^3];
        using var request = new HttpRequestMessage(HttpMethod.Get, root + "/key/info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var response = await http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
                return null;
            var info = Property(
                JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(timeout.Token)),
                "info");
            return Decimal(info, "spend") is { } spend && Decimal(info, "max_budget") is { } maxBudget
                ? new LlmKeySpend(spend, maxBudget, now)
                : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
                                          || exception is OperationCanceledException &&
                                          !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    // LiteLLM отказывает по бюджету ключа кодом 400 (иногда 429) с типом budget_exceeded: это не временный сбой.
    private static bool BudgetExceeded(string raw) =>
        raw.Contains("budget_exceeded", StringComparison.OrdinalIgnoreCase)
        || raw.Contains("budget", StringComparison.OrdinalIgnoreCase) &&
        raw.Contains("exceeded", StringComparison.OrdinalIgnoreCase);

    private static string Error(int status, string raw) => BudgetExceeded(raw)
        ? "budget_exhausted"
        : status switch
        {
            401 or 403 => "llm_key_required",
            429 => "llm_rate_limited",
            >= 500 => "llm_unavailable",
            _ => "llm_request_rejected"
        };

    private static JsonElement Property(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var child) ? child : default;

    private static string? String(JsonElement value, string name) =>
        Property(value, name) is { ValueKind: JsonValueKind.String } child
            ? child.GetString()
            : null;

    private static long? Long(JsonElement value, string name) =>
        Property(value, name) is { ValueKind: JsonValueKind.Number } child
        && child.TryGetInt64(out var number) && number >= 0
            ? number
            : null;

    private static decimal? Decimal(JsonElement value, string name) =>
        Property(value, name) is { ValueKind: JsonValueKind.Number } child
        && child.TryGetDecimal(out var number) && number >= 0
            ? number
            : null;

    public void Dispose()
    {
        http.Dispose();
        keyGate.Dispose();
    }
}