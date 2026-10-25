namespace AlfaNews.Core.Processing;

// Defer: задание не выполнено сейчас и возвращается в очередь за другими заданиями того же приоритета, попытка не расходуется.
public sealed record JobResult(
    string Stage = "done",
    string? ResultType = null,
    string? ResultId = null,
    string? ErrorCode = null,
    bool Defer = false);