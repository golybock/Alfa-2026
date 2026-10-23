namespace AlfaNews.Core.Feed;

// Какой снимок ленты читает сайт: живой или заранее посчитанный для демонстрации (PrecomputeDemo).
public sealed class FeedSettings
{
    public const string Live = "live";
    public const string Demo = "demo";

    public string Mode { get; set; } = Live;

    public void Validate()
    {
        if (Mode is not (Live or Demo))
            throw new InvalidOperationException("Feed:Mode должен быть live или demo.");
    }
}