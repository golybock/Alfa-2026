namespace AlfaNews.Core.Processing;

// Число исполнителей каждой полосы Worker. Полоса data остаётся последовательной: импорт, профили и пересчёт ленты зависят от порядка.
public sealed class QueueOptions
{
    public int NewsSlots { get; set; } = 4;
    public int DocumentsSlots { get; set; } = 3;
    public int InsightsSlots { get; set; } = 2;
    public int ChatSlots { get; set; } = 2;

    public IReadOnlyDictionary<string, int> LaneSlots() => new Dictionary<string, int>
    {
        [JobKinds.DataLane] = 1,
        [JobKinds.NewsLane] = NewsSlots,
        [JobKinds.DocumentsLane] = DocumentsSlots,
        [JobKinds.InsightsLane] = InsightsSlots,
        [JobKinds.ChatLane] = ChatSlots
    };

    public void Validate()
    {
        if (new[] { NewsSlots, DocumentsSlots, InsightsSlots, ChatSlots }.Any(slots => slots is < 1 or > 16))
            throw new InvalidOperationException("Число исполнителей полосы Queue должно быть от 1 до 16.");
    }
}