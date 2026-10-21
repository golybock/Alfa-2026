using AlfaNews.Contracts.Domain;

namespace AlfaNews.Contracts.Processing;

public sealed record GetPaymentArguments(PaymentDirection Direction, string? Okved2);