using System.Text.Json;
using AlfaNews.Contracts;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Profiles;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Tests;

public sealed class ProfileDatabaseTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [PostgresFact]
    public async Task KeepsFullDenominatorsSeparatesDirectionsAndBuildsIncompleteGroups()
    {
        using var input = new ProfileInput();
        await using var db = fixture.CreateDb();
        await input.SeedAsync(db);
        var result = await new ProfileBuilder(db).BuildAsync(input.Job, input.ReportPath, false, CancellationToken.None);
        Assert.False(result.Skipped);
        var a = await ReadCompanyAsync(db, input.A);
        Assert.Equal(100, a.Outgoing.TotalExternalAmount);
        Assert.Equal(60, a.Outgoing.KnownSectorAmount);
        Assert.Equal(.6m, a.Outgoing.Coverage);
        Assert.Equal(.24m, a.Outgoing.SectorShares["46"]);
        Assert.Equal(.36m, a.Outgoing.SectorShares["10"]);
        Assert.Equal(40, a.Incoming.TotalExternalAmount);
        Assert.Equal(20, a.Incoming.KnownSectorAmount);
        Assert.Equal(.5m, a.Incoming.Coverage);
        Assert.Equal(.3m, a.Incoming.SectorShares["46"]);
        Assert.Equal(.2m, a.Incoming.SectorShares["10"]);
        Assert.Equal(a.Outgoing.Coverage, a.Outgoing.SectorShares.Values.Sum());
        var noFlow = await ReadCompanyAsync(db, input.D);
        Assert.Equal(0, noFlow.Incoming.TotalExternalAmount);
        Assert.Equal(0, noFlow.Outgoing.Coverage);
        Assert.Empty(noFlow.Outgoing.SectorShares);
        var group = await ReadGroupAsync(db, input.Group);
        Assert.Equal(15000000000m, group.KnownRevenueSum);
        Assert.Equal(2, group.RevenueKnownCount);
        Assert.Equal(2, group.MemberIds.Count);
        var unknownGroup = await ReadGroupAsync(db, input.UnknownGroup);
        Assert.Null(unknownGroup.KnownRevenueSum);
        Assert.Equal(0, unknownGroup.RevenueKnownCount);
        Assert.Equal(2, unknownGroup.MemberIds.Count);
        Assert.Equal(a.DataVersion, group.DataVersion);
        Assert.Equal(a.DataVersion, result.Report.DataVersion);
        Assert.Empty(result.Report.Sectors);
        Assert.Contains("НОВАЯ СТРАНА", result.Report.UnmappedCountries);
        Assert.All(result.Report.SensitivityDraft, draft =>
        {
            Assert.Equal("draft", draft.Status);
            Assert.Null(draft.Weight);
            Assert.Null(draft.Sign);
        });
        Assert.Empty(result.Report.SensitivityDraft);
    }

    [PostgresFact]
    public async Task RepeatedBuildIsStableAndStartupRepairsMissingProfilesAndReport()
    {
        using var input = new ProfileInput();
        await using var db = fixture.CreateDb();
        await input.SeedAsync(db);
        var builder = new ProfileBuilder(db);
        var first = await builder.BuildAsync(input.Job, input.ReportPath, false, CancellationToken.None);
        var initialReport = await File.ReadAllTextAsync(input.ReportPath);
        Assert.Contains("Профили отражают", initialReport);
        Assert.DoesNotContain("\\u041F", initialReport);
        var initialTime = await db.CompanyProfileRows.Where(row => row.CompanyId == input.A).Select(row => row.UpdatedAt).SingleAsync();
        var repeat = await builder.BuildAsync(input.Job, input.ReportPath, false, CancellationToken.None);
        Assert.Equal(first.Report.DataVersion, repeat.Report.DataVersion);
        Assert.Equal(initialReport, await File.ReadAllTextAsync(input.ReportPath));
        Assert.Equal(initialTime, await db.CompanyProfileRows.Where(row => row.CompanyId == input.A).Select(row => row.UpdatedAt).SingleAsync());
        Assert.True((await builder.BuildAsync(input.Job, input.ReportPath, true, CancellationToken.None)).Skipped);
        await File.WriteAllTextAsync(input.ReportPath, JsonSerializer.Serialize(first.Report, JsonOptions));
        Assert.Contains("\\u041F", await File.ReadAllTextAsync(input.ReportPath));
        Assert.False((await builder.BuildAsync(input.Job, input.ReportPath, true, CancellationToken.None)).Skipped);
        Assert.Equal(initialReport, await File.ReadAllTextAsync(input.ReportPath));
        await db.CompanyProfileRows.Where(row => row.CompanyId == input.A).ExecuteDeleteAsync();
        Assert.False((await builder.BuildAsync(input.Job, input.ReportPath, true, CancellationToken.None)).Skipped);
        File.Delete(input.ReportPath);
        Assert.False((await builder.BuildAsync(input.Job, input.ReportPath, true, CancellationToken.None)).Skipped);
        Assert.Equal(initialReport, await File.ReadAllTextAsync(input.ReportPath));
    }

    [PostgresFact]
    public async Task StartupRemovesStaleProfilesFromTheSameBuildScope()
    {
        using var input = new ProfileInput();
        await using var db = fixture.CreateDb();
        await input.SeedAsync(db);
        var builder = new ProfileBuilder(db);
        var first = await builder.BuildAsync(input.Job, input.ReportPath, false, CancellationToken.None);
        var staleGroup = input.Job.DatasetVersion + "_stale";
        db.CompanyProfileRows.Add(new CompanyProfileEntity
        {
            CompanyId = "test_company_b",
            DataVersion = first.Report.DataVersion,
            ProfileJson = JsonSerializer.SerializeToElement(new { }),
            UpdatedAt = DateTimeOffset.UtcNow
        });
        db.GroupProfileRows.Add(new GroupProfileEntity
        {
            GroupId = staleGroup,
            DataVersion = first.Report.DataVersion,
            ProfileJson = JsonSerializer.SerializeToElement(new { }),
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var result = await builder.BuildAsync(input.Job, input.ReportPath, true, CancellationToken.None);
        Assert.False(result.Skipped);
        Assert.False(await db.CompanyProfileRows.AnyAsync(row => row.CompanyId == "test_company_b"));
        Assert.False(await db.GroupProfileRows.AnyAsync(row => row.GroupId == staleGroup));
    }

    [PostgresFact]
    public async Task SourceChangesRebuildProfilesAndRemoveObsoleteGroups()
    {
        using var input = new ProfileInput();
        await using var db = fixture.CreateDb();
        await input.SeedAsync(db);
        var builder = new ProfileBuilder(db);
        var first = await builder.BuildAsync(input.Job, input.ReportPath, false, CancellationToken.None);
        await db.PaymentRows.Where(row => row.Payer == input.A && row.Beneficiary == input.B)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.PayAmt, 48m));
        await db.CompanyRows.Where(row => row.Id == input.C || row.Id == input.D)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.GroupId, (string?)null));
        var changed = await builder.BuildAsync(input.Job, input.ReportPath, true, CancellationToken.None);
        Assert.False(changed.Skipped);
        Assert.NotEqual(first.Report.DataVersion, changed.Report.DataVersion);
        var a = await ReadCompanyAsync(db, input.A);
        Assert.Equal(124, a.Outgoing.TotalExternalAmount);
        Assert.Equal(48m / 124m, a.Outgoing.SectorShares["46"]);
        Assert.False(await db.GroupProfileRows.AnyAsync(row => row.GroupId == input.UnknownGroup));
        Assert.Equal(1, changed.Report.GroupCount);
    }

    [PostgresFact]
    public async Task ReportFailureRollsBackBothProfileTablesAndCanBeRetried()
    {
        using var input = new ProfileInput();
        await using var db = fixture.CreateDb();
        await input.SeedAsync(db);
        Directory.CreateDirectory(input.ReportPath);
        var builder = new ProfileBuilder(db);
        var error = await Record.ExceptionAsync(() => builder.BuildAsync(input.Job, input.ReportPath, false, CancellationToken.None));
        Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString());
        Assert.False(await db.CompanyProfileRows.AnyAsync(row => row.CompanyId == input.A));
        Assert.False(await db.GroupProfileRows.AnyAsync(row => row.GroupId == input.Group));
        Directory.Delete(input.ReportPath);
        Assert.False((await builder.BuildAsync(input.Job, input.ReportPath, true, CancellationToken.None)).Skipped);
    }

    [PostgresFact]
    public async Task ConcurrentStartupBuildsOnce()
    {
        using var input = new ProfileInput();
        await using var seed = fixture.CreateDb();
        await input.SeedAsync(seed);
        await using var first = fixture.CreateDb();
        await using var second = fixture.CreateDb();
        var results = await Task.WhenAll(
            new ProfileBuilder(first).BuildAsync(input.Job, input.ReportPath, true, CancellationToken.None),
            new ProfileBuilder(second).BuildAsync(input.Job, input.ReportPath, true, CancellationToken.None));
        Assert.Single(results, result => result.Skipped);
        Assert.Single(results, result => !result.Skipped);
        Assert.Equal(results[0].Report.DataVersion, results[1].Report.DataVersion);
    }

    [PostgresFact]
    public async Task RejectsMissingInputsAndMismatchedPaymentVersions()
    {
        using var input = new ProfileInput();
        await using var db = fixture.CreateDb();
        var builder = new ProfileBuilder(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => builder.BuildAsync(input.Job, input.ReportPath, true, CancellationToken.None));
        await input.SeedAsync(db);
        await db.PaymentRows.Where(row => row.Payer == input.A && row.Beneficiary == input.B)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.DatasetVersion, "other"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => builder.BuildAsync(input.Job, input.ReportPath, true, CancellationToken.None));
        Assert.False(await db.CompanyProfileRows.AnyAsync(row => row.CompanyId == input.A));
        Assert.False(File.Exists(input.ReportPath));
    }

    [PostgresFact]
    public async Task MissingSectorAndZeroAmountsDoNotInventCoverage()
    {
        using var input = new ProfileInput();
        await using var db = fixture.CreateDb();
        await input.SeedAsync(db);
        await db.PaymentRows.Where(row => row.DatasetVersion == input.Job.DatasetVersion).ExecuteDeleteAsync();
        await db.CompanyRows.Where(row => row.Id == input.B).ExecuteUpdateAsync(update => update.SetProperty(row => row.MainOkvedCode, ""));
        db.ChangeTracker.Clear();
        db.PaymentRows.AddRange(
            new PaymentEntity { Payer = input.A, Beneficiary = input.B, PayAmt = 20, PayCnt = 1, DatasetVersion = input.Job.DatasetVersion },
            new PaymentEntity { Payer = input.A, Beneficiary = input.C, PayAmt = 0, PayCnt = 1, DatasetVersion = input.Job.DatasetVersion });
        await db.SaveChangesAsync();
        await new ProfileBuilder(db).BuildAsync(input.Job, input.ReportPath, false, CancellationToken.None);
        var a = await ReadCompanyAsync(db, input.A);
        Assert.Equal(20, a.Outgoing.TotalExternalAmount);
        Assert.Equal(0, a.Outgoing.KnownSectorAmount);
        Assert.Equal(0, a.Outgoing.Coverage);
        Assert.Empty(a.Outgoing.SectorShares);
    }

    private static async Task<CompanyProfile> ReadCompanyAsync(AlfaNewsDbContext db, string id)
    {
        var row = await db.CompanyProfileRows.AsNoTracking().SingleAsync(profile => profile.CompanyId == id);
        return row.ProfileJson.Deserialize<CompanyProfile>(JsonOptions)!;
    }

    private static async Task<GroupProfile> ReadGroupAsync(AlfaNewsDbContext db, string id)
    {
        var row = await db.GroupProfileRows.AsNoTracking().SingleAsync(profile => profile.GroupId == id);
        return row.ProfileJson.Deserialize<GroupProfile>(JsonOptions)!;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions();
        ContractJson.Configure(options);
        return options;
    }

    private sealed class ProfileInput : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "alfanews_profiles_" + Guid.NewGuid().ToString("N"));
        public RebuildProfilesJob Job { get; } = new(Guid.NewGuid().ToString("N"));
        public string A => Job.DatasetVersion + "_a";
        public string B => Job.DatasetVersion + "_b";
        public string C => Job.DatasetVersion + "_c";
        public string D => Job.DatasetVersion + "_d";
        public string Group => Job.DatasetVersion + "_group";
        public string UnknownGroup => Job.DatasetVersion + "_unknown_group";
        public string ReportPath => Path.Combine(directory, "profile_candidates.json");

        public ProfileInput() => Directory.CreateDirectory(directory);

        public async Task SeedAsync(AlfaNewsDbContext db)
        {
            var raw = JsonSerializer.SerializeToElement(new Dictionary<string, string>
            {
                ["CL_PRODUCT_LOA_MARK"] = "1.0",
                ["FTS_COUNTRY_NAME"] = "КИТАЙ, Новая страна"
            });
            var empty = JsonSerializer.SerializeToElement(new { });
            db.CompanyRows.AddRange(
                Company(A, "01.11", Group, 8000000000m, raw), Company(B, "46.71", Group, 7000000000m, empty),
                Company(C, "10.11", UnknownGroup, null, empty), Company(D, "64.19", UnknownGroup, null, empty));
            var unknown = Job.DatasetVersion + "_external";
            db.PaymentRows.AddRange(Payment(A, B, 24), Payment(A, C, 36), Payment(A, unknown, 40),
                Payment(B, A, 12), Payment(C, A, 8), Payment(unknown, A, 20), Payment(A, unknown + "_negative", -900));
            await db.SaveChangesAsync();
        }

        private CompanyEntity Company(string id, string okved, string? group, decimal? revenue, JsonElement raw) => new()
        {
            Id = id,
            ClType = "CLIENT",
            MainOkvedCode = okved,
            MainOkvedName = "Тестовая отрасль",
            GroupId = group,
            Revenue = revenue,
            RawData = raw,
            DatasetVersion = Job.DatasetVersion,
            ImportedAt = DateTimeOffset.UtcNow
        };

        private PaymentEntity Payment(string payer, string beneficiary, decimal amount) => new()
        {
            Payer = payer,
            Beneficiary = beneficiary,
            PayAmt = amount,
            PayCnt = 1,
            DatasetVersion = Job.DatasetVersion
        };

        public void Dispose() => Directory.Delete(directory, true);
    }
}