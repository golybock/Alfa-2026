using System.Text.Json.Serialization;

namespace AlfaNews.Contracts.Domain;

public sealed record InsightDto(
	string Id,
	string EventId,
	string CompanyId,
	Role Role,
	DateTimeOffset AsOf,
	string Headline,
	RelevanceDto Relevance,
	IReadOnlyList<string> WhatHappened,
	IReadOnlyList<string> WhyItMatters,
	IReadOnlyList<ConsequenceDto> Consequences,
	IReadOnlyList<string> Actions,
	BankViewDto BankView,
	IReadOnlyList<ClaimDto> Claims,
	IReadOnlyDictionary<string, EvidenceDto> Evidence,
	IReadOnlyList<string> Limitations,
	IReadOnlyList<string> SuggestedQuestions,
	string TraceId,
	VerificationDto Verification,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	ForecastDto? Forecast = null,
	ConsequenceType? Direction = null,
	DateTimeOffset? NewsPeriodFrom = null,
	DateTimeOffset? NewsPeriodTo = null,
	IReadOnlyList<CompanyTraitDto>? CompanyTraits = null,
	DateOnly? BankDataAsOf = null,
	string? WhyYou = null
);