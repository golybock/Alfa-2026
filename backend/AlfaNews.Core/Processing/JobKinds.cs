using System.Text.Json;
using AlfaNews.Contracts;
using AlfaNews.Contracts.Processing;

namespace AlfaNews.Core.Processing;

public static class JobKinds
{
    public const string DataLane = "data";
    public const string NewsLane = "news";
    public const string DocumentsLane = "documents";
    public const string InsightsLane = "insights";
    public const string ChatLane = "chat";

    private static readonly IReadOnlyDictionary<Type, (int Priority, string Lane)> Kinds = new Dictionary<Type, (int, string)>
    {
        [typeof(ImportCsvJob)] = (100, DataLane),
        [typeof(RebuildProfilesJob)] = (90, DataLane),
        [typeof(CollectNewsJob)] = (80, NewsLane),
        [typeof(CollectManualNewsJob)] = (80, NewsLane),
        [typeof(ParseNewsJob)] = (83, NewsLane),
        [typeof(TriageNewsJob)] = (82, NewsLane),
        [typeof(RefreshFeedJob)] = (70, DataLane),
        [typeof(GenerateInsightJob)] = (60, InsightsLane),
        [typeof(AnswerChatJob)] = (60, ChatLane),
        [typeof(FinalizePdfJob)] = (30, DocumentsLane),
        [typeof(IndexPdfJob)] = (30, DocumentsLane),
        [typeof(EmbedContentJob)] = (20, DocumentsLane),
        [typeof(PrecomputeDemoJob)] = (10, InsightsLane),
        [typeof(OcrPdfPageJob)] = (0, DocumentsLane),
        [typeof(ConsoleMessageJob)] = (-10, DataLane)
    };

    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    public static string Name<T>() where T : class => Name(typeof(T));

    public static string Name(Type type)
    {
        if (!Kinds.ContainsKey(type))
            throw new ArgumentException("Неизвестный тип фонового задания.", nameof(type));
        return type.Name[..^3];
    }

    public static int Priority<T>() where T : class => Kinds[typeof(T)].Priority;

    public static Type? PayloadType(string kind) => Kinds.Keys.SingleOrDefault(type => Name(type) == kind);

    public static string[] InLane(string lane) => Kinds.Where(pair => pair.Value.Lane == lane).Select(pair => Name(pair.Key)).ToArray();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions();
        ContractJson.Configure(options);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}