using System.Text.Json;
using AlfaNews.Contracts.Processing;

namespace AlfaNews.Core.Llm;

public sealed record LlmRequest(
    Guid JobId,
    string OperationKey,
    string InputVersion,
    LlmTask Task,
    IReadOnlyDictionary<string, JsonElement> Values,
    Guid? CardId = null,
    LlmImage? Image = null,
    Func<JsonElement, IReadOnlyList<string>>? ValidateOutput = null,
    string ValidationVersion = "v1",
    LlmRuntime? Runtime = null,
    Action<string>? CaptureResponse = null,
    Guid? RunId = null);

public sealed record LlmImage(byte[] Bytes, string MediaType = "image/png");

public sealed record LlmRuntime(
    IReadOnlyDictionary<string, JsonElement>? Tools = null,
    int RemainingToolCalls = 0,
    bool ForecastEnabled = false,
    int? Rank = null);

// Dropped — что убрала очистка ответа (LlmSalvage): «claim:C3:number_not_in_evidence», «fact:4:number_not_in_quote».
public sealed record LlmResult<T>(
    T Value,
    Guid RunId,
    bool CacheHit,
    string CacheKey,
    IReadOnlyList<string>? Dropped = null);

public sealed class LlmException(string code, IReadOnlyList<string>? diagnostics = null) : Exception(code)
{
    public string Code { get; } = code;
    public IReadOnlyList<string> Diagnostics { get; } = diagnostics ?? [];
}