namespace AlfaNews.Contracts.Processing;

public sealed record EmbedContentJob(string EntityType, string EntityId, string Version);