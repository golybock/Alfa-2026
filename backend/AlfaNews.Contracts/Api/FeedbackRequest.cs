using AlfaNews.Contracts.Domain;

namespace AlfaNews.Contracts.Api;

public sealed record FeedbackRequest(FeedbackKind Kind, FeedbackReason? Reason);