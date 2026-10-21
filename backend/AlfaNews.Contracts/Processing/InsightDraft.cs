using System.Text.Json.Serialization;
using AlfaNews.Contracts.Domain;

namespace AlfaNews.Contracts.Processing;

public sealed record InsightDraft(
	string Headline,
	IReadOnlyList<string> WhatHappened,
	IReadOnlyList<string> WhyItMatters,
	IReadOnlyList<ConsequenceDto> Consequences,
	IReadOnlyList<string> Actions,
	BankViewDto BankView,
	IReadOnlyList<DraftClaim> Claims,
	IReadOnlyList<string> Limitations,
	IReadOnlyList<string> SuggestedQuestions,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	ForecastDto? Forecast = null
);