using System.Text.Json;
using AlfaNews.Contracts;
using AlfaNews.Contracts.Processing;

namespace AlfaNews.Core.Processing;

public static class JobKinds
{
    private static readonly IReadOnlyDictionary<Type, int> Priorities = new Dictionary<Type, int>
    {
        [typeof(ImportCsvJob)] = 100,
        [typeof(RebuildProfilesJob)] = 90,
        [typeof(CollectNewsJob)] = 80,
        [typeof(CollectManualNewsJob)] = 80,
        [typeof(ParseNewsJob)] = 83,
        [typeof(TriageNewsJob)] = 82,
        [typeof(RefreshFeedJob)] = 70,
        [typeof(GenerateInsightJob)] = 60,
        [typeof(AnswerChatJob)] = 60,
        [typeof(FinalizePdfJob)] = 30,
        [typeof(IndexPdfJob)] = 30,
        [typeof(EmbedContentJob)] = 20,
        [typeof(PrecomputeDemoJob)] = 10,
        [typeof(OcrPdfPageJob)] = 0,
        [typeof(ConsoleMessageJob)] = -10
    };

    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    public static string Name<T>() where T : class => Name(typeof(T));

    public static string Name(Type type)
    {
        if (!Priorities.ContainsKey(type))
            throw new ArgumentException("Неизвестный тип фонового задания.", nameof(type));
        return type.Name[..^3];
    }

    public static int Priority<T>() where T : class => Priorities[typeof(T)];

    public static Type? PayloadType(string kind) => Priorities.Keys.SingleOrDefault(type => Name(type) == kind);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions();
        ContractJson.Configure(options);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}