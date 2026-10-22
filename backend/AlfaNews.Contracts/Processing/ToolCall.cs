using System.Text.Json;

namespace AlfaNews.Contracts.Processing;

public sealed record ToolCall(string Name, JsonElement Arguments);