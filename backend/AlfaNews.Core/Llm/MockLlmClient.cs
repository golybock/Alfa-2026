namespace AlfaNews.Core.Llm;

public sealed class MockLlmClient : ILlmClient
{
    public async Task<LlmClientResponse> SendAsync(LlmPrompt prompt, LlmRoute route, LlmImage? image,
        CancellationToken cancellationToken)
    {
        if (prompt.SchemaName == "NewsTriage")
        {
            var start = prompt.User.IndexOf("<input_json>", StringComparison.Ordinal) + "<input_json>".Length;
            var end = prompt.User.LastIndexOf("</input_json>", StringComparison.Ordinal);
            using var input = System.Text.Json.JsonDocument.Parse(prompt.User[start..end]);
            var decisions = input.RootElement.EnumerateArray().Select(item => new { id = item.GetProperty("id").GetString(), importance = "maybe" });
            return new LlmClientResponse(System.Text.Json.JsonSerializer.Serialize(new { decisions }), ActualCostRub: 0, FinishReason: "stop");
        }
        var path = Path.Combine(AppContext.BaseDirectory, "Prompts", "fixtures", prompt.SchemaName + ".json");
        return new LlmClientResponse(await File.ReadAllTextAsync(path, cancellationToken), ActualCostRub: 0, FinishReason: "stop");
    }
}