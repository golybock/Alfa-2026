using System.Text.Json;

namespace AlfaNews.Core.Llm;

public interface ILlmClient
{
    Task<LlmClientResponse> SendAsync(LlmPrompt prompt, LlmRoute route, LlmImage? image, CancellationToken cancellationToken);
}

public sealed record LlmClientResponse(string? Content = null, string? Error = null, bool Transient = false,
    long InputTokens = 0, long OutputTokens = 0, long? ReasoningTokens = null, decimal? ActualCostRub = null,
    int? HttpStatus = null, string? FinishReason = null);