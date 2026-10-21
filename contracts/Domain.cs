using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlfaNews.Contracts;

public enum Role { Ceo, Cfo }
public enum Origin { SourceFact, BankAnalytics, AiInference, AiHypothesis }
public enum EvidenceType { News, BankResearch, CompanyFeature, PaymentAggregate, GroupFeature }
public enum ClaimVerification { CodePassed, CriticPassed, NotConfirmed }
public enum Confidence { Low, Medium, High }
public enum AnalysisStatus { Queued, Processing, Ready, Failed }
public enum JobStatus { Queued, Running, Done, Failed }
public enum OcrStatus { Queued, Processing, Ready, Partial, Failed }
public enum ReviewStatus { NotReviewed, PartiallyReviewed, Reviewed }
public enum CriticStatus { Passed, IssuesFound, SkippedDisabled, SkippedBudget, SkippedError }
public enum BankViewStatus { Found, NotFound }
public enum ConsequenceType { Risk, Opportunity }
public enum PaymentDirection { Incoming, Outgoing }
public enum RefreshStatus { Idle, Queued, Processing, Failed }
public enum ChatAnswerStatus { Answer, Refusal }
public enum FeedbackKind { Like, Dislike, Hide }
public enum FeedbackReason { NotRelevant, WrongFact, Obvious }
public enum CodeStatus { Passed, Failed }
public enum ResearchBlockKind { Fact, BankView, Forecast, Disclaimer }
public enum ForecastStatus { Estimated, InsufficientData }
public enum ScopeLabel { InScope, OutOfScope, Injection }

public sealed record CompanyProfile(
    string CompanyId, string? GroupId, string OkvedCode, string Okved2,
    string? IndustryName, string? Region, string? City, string? AgeBand,
    string? Lifecycle, decimal? Revenue, IReadOnlyList<string> Products,
    IReadOnlyList<string> FtsCountries, PaymentExposure Incoming,
    PaymentExposure Outgoing, string DataVersion);
public sealed record PaymentExposure(
    decimal TotalExternalAmount, decimal KnownSectorAmount, decimal Coverage,
    IReadOnlyDictionary<string, decimal> SectorShares);
public sealed record GroupProfile(
    string GroupId, IReadOnlyList<string> MemberIds, decimal? KnownRevenueSum,
    int RevenueKnownCount, string DataVersion);
public sealed record FeatureContribution(
    string Code, decimal Value, decimal Weight, decimal Contribution,
    bool Known, IReadOnlyList<string> EvidenceRefs);
public sealed record RankingResult(
    string CompanyId, string EventId, decimal Base, decimal Recency,
    decimal SourceReliability, bool HasBankView, decimal Score, int Rank,
    bool Eligible, string ConfigVersion, IReadOnlyList<FeatureContribution> Features);
public sealed record RelevanceReasonDto(
    string Code, string Text, IReadOnlyList<string> ClaimIds,
    IReadOnlyList<string> EvidenceRefs);
public sealed record RelevanceDto(
    decimal Score, int Rank, IReadOnlyList<RelevanceReasonDto> Reasons,
    IReadOnlyList<FeatureContribution> Contributions);
public sealed record ClaimDto(
    string Id, string Text, Origin Origin, IReadOnlyList<string> Refs,
    ClaimVerification VerificationStatus, Confidence? Confidence);
public sealed record EvidenceDto(
    EvidenceType Type, string Source, string? Url, string? Quote,
    DateOnly? Date, string? DocumentId, int? Page, string? ChunkId,
    string? Key, JsonElement? Value, string? Dataset,
    ReviewStatus? ReviewStatus, bool Approximate, string? ChartId,
    string? PageImageUrl);
public sealed record ConsequenceDto(ConsequenceType Type, IReadOnlyList<string> ClaimIds);
public sealed record BankViewDto(BankViewStatus Status, IReadOnlyList<string> ClaimIds);
public sealed record CriticVerificationDto(CriticStatus Status, int RequestCount, string? Reason);
public sealed record VerificationDto(CodeStatus CodeStatus, CriticVerificationDto Critic, int RepairCount);
public sealed record ForecastInputDto(
    string Name, decimal Value, string Unit, bool Assumed, IReadOnlyList<string> Refs);
public sealed record ForecastDto(
    ForecastStatus Status, string Metric, string Unit, decimal? Low, decimal? High,
    string? Formula, IReadOnlyList<ForecastInputDto> Inputs,
    IReadOnlyList<string> Assumptions, IReadOnlyList<string> MissingData,
    Origin Origin);
public sealed record InsightDto(
    string Id, string EventId, string CompanyId, Role Role, DateTimeOffset AsOf,
    string Headline, RelevanceDto Relevance, IReadOnlyList<string> WhatHappened,
    IReadOnlyList<string> WhyItMatters, IReadOnlyList<ConsequenceDto> Consequences,
    IReadOnlyList<string> Actions, BankViewDto BankView, IReadOnlyList<ClaimDto> Claims,
    IReadOnlyDictionary<string, EvidenceDto> Evidence, IReadOnlyList<string> Limitations,
    IReadOnlyList<string> SuggestedQuestions, string TraceId, VerificationDto Verification,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ForecastDto? Forecast = null);