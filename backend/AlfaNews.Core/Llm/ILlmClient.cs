using System.Text.Json;

namespace AlfaNews.Core.Llm;

public interface ILlmClient
{
    Task<LlmClientResponse> SendAsync(LlmPrompt prompt, LlmRoute route, LlmImage? image,
        CancellationToken cancellationToken);

    // Расход и дневной лимит ключа на шлюзе; null — шлюз их не сообщает, остаётся проверка по своей базе.
    Task<LlmKeySpend?> KeySpendAsync(CancellationToken cancellationToken) => Task.FromResult<LlmKeySpend?>(null);
}

// Ключ общий на команду: Spend — расход всех участников за сутки шлюза, FetchedAt — когда шлюз его сообщил.
public sealed record LlmKeySpend(decimal Spend, decimal MaxBudget, DateTimeOffset FetchedAt);

public sealed record LlmClientResponse(
    string? Content = null,
    string? Error = null,
    bool Transient = false,
    long InputTokens = 0,
    long OutputTokens = 0,
    long? ReasoningTokens = null,
    decimal? ActualCostRub = null,
    int? HttpStatus = null,
    string? FinishReason = null);