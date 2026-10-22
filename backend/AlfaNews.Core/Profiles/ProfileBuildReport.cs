using AlfaNews.Contracts.Domain;

namespace AlfaNews.Core.Profiles;

public sealed record ProfileBuildResult(bool Skipped, ProfileBuildReport Report);

public sealed record ProfileBuildReport(
    string DatasetVersion,
    string DataVersion,
    string AlgorithmVersion,
    int CompanyCount,
    int GroupCount,
    IReadOnlyList<DemoSectorCandidates> Sectors,
    IReadOnlyList<SensitivityDraft> SensitivityDraft,
    IReadOnlyList<string> UnmappedCountries,
    IReadOnlyList<string> Limitations);

public sealed record DemoSectorCandidates(string Okved2, IReadOnlyList<DemoCandidate> Candidates);

public sealed record DemoCandidate(CompanyProfile Company, GroupProfile? Group, IReadOnlyList<string> Limitations);

public sealed record SensitivityDraft(
    string Okved2,
    string Topic,
    string? FactorCode,
    string Channel,
    string VerificationQuestion,
    decimal? Weight,
    string? Sign,
    string Status);