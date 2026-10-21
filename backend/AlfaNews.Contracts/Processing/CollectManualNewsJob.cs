using AlfaNews.Contracts.Api;

namespace AlfaNews.Contracts.Processing;

public sealed record CollectManualNewsJob(ManualNewsRequest Request);