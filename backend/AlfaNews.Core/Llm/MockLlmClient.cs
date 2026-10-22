namespace AlfaNews.Core.Llm;

public sealed class MockLlmClient : ILlmClient
{
    public async Task<LlmClientResponse> SendAsync(LlmPrompt prompt, LlmRoute route, LlmImage? image,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Prompts", "fixtures", prompt.SchemaName + ".json");
        return new LlmClientResponse(await File.ReadAllTextAsync(path, cancellationToken), ActualCostRub: 0, FinishReason: "stop");
    }
}