using Microsoft.Extensions.DependencyInjection;

namespace AlfaNews.Core.Llm;

public static class LlmRegistration
{
    public static IServiceCollection AddAlfaNewsLlm(this IServiceCollection services, LlmOptions options)
    {
        options.Validate();
        services.AddSingleton(options);
        services.AddSingleton<LlmPromptCatalog>();
        services.AddSingleton<LlmBudget>();
        services.AddSingleton<ILlmClient>(_ => options.UseMock
            ? new MockLlmClient()
            : new OpenAiApiClient(new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
                { Timeout = Timeout.InfiniteTimeSpan }, options));
        services.AddScoped<LlmGateway>();
        return services;
    }
}