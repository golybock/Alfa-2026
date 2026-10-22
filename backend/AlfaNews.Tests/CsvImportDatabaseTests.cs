using System.Text;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Importing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace AlfaNews.Tests;

public sealed class CsvImportDatabaseTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task CleansAndReimportsWithoutDoublingAndSkipsCompletedStartup()
    {
        using var package = new TestPackage();
        await package.WriteAsync([
            Company("csv_a", revenue: "1000"), Company("csv_a", revenue: "1000"),
            Company("csv_b", revenue: "1500"), Company("csv_conflict", revenue: "10"),
            Company("csv_conflict", revenue: "20"), Company("csv_prospect", type: "PROSPECT"),
            Company("csv_bad_flag", flag: "true"), Company("csv_bad_okved", okved: "abc"),
            Company("csv_bad_revenue", revenue: "-5")
        ], ["csv_b;csv_a;10.25;1.0", "csv_b;csv_a;20.25;2", "csv_unknown;csv_a;5;1",
            "csv_a;csv_a;100;2", "csv_b;csv_a;-3;1", "csv_b;csv_a;bad;1", "csv_b;csv_a;4;-1"]);
        await using var db = fixture.CreateDb();
        var importer = new CsvImporter(db);
        var first = await importer.ImportAsync(package.Job, package.ReportPath, false, CancellationToken.None);
        Assert.False(first.Skipped);
        var report = Assert.IsType<CsvImportReport>(first.Report);
        Assert.Equal(9, report.Companies.Read);
        Assert.Equal(2, report.Companies.Accepted);
        Assert.Equal(2, report.Companies.Excluded["conflicting_id"]);
        Assert.Equal(1, report.Companies.Excluded["identical_duplicate"]);
        Assert.Equal(1, report.Companies.Excluded["invalid_flag"]);
        Assert.Equal(1, report.Companies.Quality["revenue_placeholder"]);
        Assert.Equal(2, report.Payments.Accepted);
        Assert.Equal(3, report.Payments.AcceptedSourceRows);
        Assert.Equal(1, report.Payments.MergedRows);
        Assert.Equal(2, report.Payments.Excluded["invalid_or_negative_amount"]);
        Assert.Equal(1, report.Payments.Quality["pairs_with_missing_profile"]);
        var company = await db.CompanyRows.SingleAsync(row => row.Id == "csv_a");
        Assert.Null(company.Revenue);
        Assert.Equal("1000", company.RawData.GetProperty("REVENUE_COMPANY").GetString());
        Assert.Equal(24, company.RawData.EnumerateObject().Count());
        Assert.Equal("01.11", company.MainOkvedCode);
        Assert.False(await db.CompanyRows.AnyAsync(row => row.Id == "csv_conflict"));
        var payment = await db.PaymentRows.AsNoTracking().SingleAsync(row => row.Payer == "csv_a" && row.Beneficiary == "csv_b");
        Assert.Equal(30.50m, payment.PayAmt);
        Assert.Equal(3, payment.PayCnt);
        Assert.True(await db.PaymentRows.AnyAsync(row => row.Beneficiary == "csv_unknown"));
        var reportText = await File.ReadAllTextAsync(package.ReportPath);
        await importer.ImportAsync(package.Job, package.ReportPath, false, CancellationToken.None);
        Assert.Equal(reportText, await File.ReadAllTextAsync(package.ReportPath));
        Assert.Equal(35.50m, await db.PaymentRows.Where(row => row.DatasetVersion == package.Job.DatasetVersion).SumAsync(row => row.PayAmt));
        File.Delete(package.Job.CompanyFile);
        File.Delete(package.Job.PaymentFile);
        Assert.True((await importer.ImportAsync(package.Job, package.ReportPath, true, CancellationToken.None)).Skipped);
    }

    [PostgresFact]
    public async Task FailedPackageRollsBackAndCanBeRetried()
    {
        using var package = new TestPackage();
        await package.WriteAsync([Company("csv_rollback")], ["b;a;1;1"]);
        await File.WriteAllTextAsync(package.Job.PaymentFile, "wrong;header");
        await using var db = fixture.CreateDb();
        var importer = new CsvImporter(db);
        await Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(package.Job, package.ReportPath, true, CancellationToken.None));
        Assert.False(await db.CompanyRows.AnyAsync(row => row.Id == "csv_rollback"));
        Assert.False(File.Exists(package.ReportPath));
        await package.WriteAsync([Company("csv_rollback")], ["b;a;1;1"]);
        Assert.False((await importer.ImportAsync(package.Job, package.ReportPath, true, CancellationToken.None)).Skipped);
        await File.AppendAllTextAsync(package.Job.PaymentFile, "\nb;a;2;1");
        await Assert.ThrowsAsync<InvalidOperationException>(() => importer.ImportAsync(package.Job, package.ReportPath, false, CancellationToken.None));
    }

    [PostgresFact]
    public async Task ConcurrentStartupImportsOnlyOnce()
    {
        using var package = new TestPackage();
        await package.WriteAsync([Company("csv_concurrent")], ["csv_concurrent_external;csv_concurrent;5;1"]);
        await using var firstDb = fixture.CreateDb();
        await using var secondDb = fixture.CreateDb();
        var results = await Task.WhenAll(
            new CsvImporter(firstDb).ImportAsync(package.Job, package.ReportPath, true, CancellationToken.None),
            new CsvImporter(secondDb).ImportAsync(package.Job, package.ReportPath, true, CancellationToken.None));
        Assert.Single(results, result => result.Skipped);
        Assert.Single(results, result => !result.Skipped);
        Assert.Equal(1, await firstDb.PaymentRows.CountAsync(row => row.DatasetVersion == package.Job.DatasetVersion));
    }

    [PostgresFact]
    public async Task ReportFailureRollsBackSavedRowsAndCompletionMarker()
    {
        using var package = new TestPackage();
        await package.WriteAsync([Company("csv_report_failure")], ["csv_report_external;csv_report_failure;5;1"]);
        Directory.CreateDirectory(package.ReportPath);
        await using var db = fixture.CreateDb();
        var importer = new CsvImporter(db);
        var error = await Record.ExceptionAsync(() => importer.ImportAsync(package.Job, package.ReportPath, true, CancellationToken.None));
        Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString());
        Assert.False(await db.CompanyRows.AnyAsync(row => row.Id == "csv_report_failure"));
        Assert.False(await db.PaymentRows.AnyAsync(row => row.DatasetVersion == package.Job.DatasetVersion));
        Directory.Delete(package.ReportPath);
        Assert.False((await importer.ImportAsync(package.Job, package.ReportPath, true, CancellationToken.None)).Skipped);
    }

    [PostgresFact]
    public async Task ReportsWrongColumnCountWithoutDroppingValidCompany()
    {
        using var package = new TestPackage();
        await package.WriteAsync([Company("csv_columns"), "csv_columns;broken"], ["external;csv_columns;2;1;extra"]);
        await using var db = fixture.CreateDb();
        var result = await new CsvImporter(db).ImportAsync(package.Job, package.ReportPath, false, CancellationToken.None);
        Assert.Equal(1, result.Report!.Companies.Accepted);
        Assert.Equal(1, result.Report.Companies.Excluded["invalid_column_count"]);
        Assert.Equal(1, result.Report.Payments.Excluded["invalid_column_count"]);
        Assert.Equal(0, result.Report.Payments.Accepted);
    }

    [PostgresFact]
    public async Task FiltersByCleanClientGroupRevenueAndKeepsUnknownRevenueAndExternalPayments()
    {
        using var package = new TestPackage();
        await package.WriteAsync([
            Company("csv_group_exact_a", revenue: "8000000000", groupId: "csv_exact"),
            Company("csv_group_exact_b", revenue: "7000000000", groupId: " csv_exact "),
            Company("csv_group_above", revenue: "15000000001", groupId: "csv_above"),
            Company("csv_group_small", revenue: "8000000000", groupId: "csv_small"),
            Company("csv_group_small", revenue: "8000000000", groupId: "csv_small"),
            Company("csv_group_placeholder", revenue: "1000", groupId: "csv_small"),
            Company("csv_group_prospect", revenue: "20000000000", type: "PROSPECT", groupId: "csv_small"),
            Company("csv_group_conflict", revenue: "20000000000", groupId: "csv_small"),
            Company("csv_group_conflict", revenue: "20000000001", groupId: "csv_small"),
            Company("csv_group_invalid", revenue: "20000000000", flag: "bad", groupId: "csv_small"),
            Company("csv_group_unknown", revenue: "", groupId: "csv_unknown_revenue"),
            Company("csv_group_unknown_placeholder", revenue: "1000", groupId: "csv_unknown_revenue"),
            Company("csv_group_missing", revenue: "10")
        ], ["csv_group_small;csv_group_exact_a;5;1"]);
        await using var db = fixture.CreateDb();
        var importer = new CsvImporter(db);
        var first = await importer.ImportAsync(package.Job, package.ReportPath, false, CancellationToken.None);
        Assert.Equal(6, first.Report!.Companies.Accepted);
        Assert.Equal(2, first.Report.Companies.Excluded["group_revenue_below_threshold"]);
        Assert.Equal(first.Report.Companies.Read, first.Report.Companies.AcceptedSourceRows + first.Report.Companies.Excluded.Values.Sum());
        Assert.False(await db.CompanyRows.AnyAsync(row => row.Id == "csv_group_small"));
        Assert.True(await db.CompanyRows.AnyAsync(row => row.Id == "csv_group_exact_b" && row.GroupId == "csv_exact"));
        Assert.True(await db.CompanyRows.AnyAsync(row => row.Id == "csv_group_unknown"));
        Assert.True(await db.CompanyRows.AnyAsync(row => row.Id == "csv_group_missing"));
        Assert.True(await db.PaymentRows.AnyAsync(row => row.Beneficiary == "csv_group_small"));
        Assert.Equal(1, first.Report.Payments.Quality["pairs_with_missing_profile"]);
        var report = await File.ReadAllTextAsync(package.ReportPath);
        await importer.ImportAsync(package.Job, package.ReportPath, false, CancellationToken.None);
        Assert.Equal(report, await File.ReadAllTextAsync(package.ReportPath));
    }

    [PostgresFact]
    public async Task ReprocessesLegacyImportOnceAndRemovesPreviouslyImportedSmallGroup()
    {
        using var package = new TestPackage();
        await package.WriteAsync([
            Company("csv_legacy_small", revenue: "100", groupId: "csv_legacy_small_group"),
            Company("csv_legacy_large", revenue: "15000000000", groupId: "csv_legacy_large_group")
        ], ["csv_legacy_small;csv_legacy_large;5;1"]);
        await using var db = fixture.CreateDb();
        var importer = new CsvImporter(db);
        await importer.ImportAsync(package.Job, package.ReportPath, false, CancellationToken.None);
        var emptyJson = "{}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO company (id, cl_type, group_id, main_okved_code, revenue, raw_data, dataset_version, imported_at)
            VALUES ('csv_legacy_small', 'CLIENT', 'csv_legacy_small_group', '01.11', 100, {emptyJson}::jsonb, {package.Job.DatasetVersion}, now());
            UPDATE background_job SET stage = 'csv_imported'
            WHERE kind = 'ImportCsv' AND payload->>'dataset_version' = {package.Job.DatasetVersion};
            """);
        Assert.False((await importer.ImportAsync(package.Job, package.ReportPath, true, CancellationToken.None)).Skipped);
        Assert.False(await db.CompanyRows.AnyAsync(row => row.Id == "csv_legacy_small"));
        Assert.True(await db.PaymentRows.AnyAsync(row => row.Beneficiary == "csv_legacy_small"));
        Assert.True((await importer.ImportAsync(package.Job, package.ReportPath, true, CancellationToken.None)).Skipped);
    }

    [PostgresFact]
    public async Task RemovesPaymentsOnlyWhenNeitherParticipantIsInFinalCompanyTable()
    {
        using var package = new TestPackage();
        await package.WriteAsync([
            Company("csv_payment_owner"), Company("csv_payment_removed", revenue: "10", groupId: "csv_payment_small")
        ], [
            "csv_payment_outside;csv_payment_owner;10;1", "csv_payment_owner;csv_payment_outside;20;2",
            "test_company_a;csv_payment_outside;30;3", "csv_payment_ghost_b;csv_payment_ghost_a;40;4",
            "csv_payment_ghost_b;csv_payment_ghost_a;50;5", "csv_payment_ghost_a;csv_payment_removed;60;6",
            "csv_payment_owner;test_company_a;70;7"
        ]);
        await using var db = fixture.CreateDb();
        var importer = new CsvImporter(db);
        var result = await importer.ImportAsync(package.Job, package.ReportPath, false, CancellationToken.None);
        var payments = result.Report!.Payments;
        Assert.Equal(7, payments.Read);
        Assert.Equal(4, payments.Accepted);
        Assert.Equal(4, payments.AcceptedSourceRows);
        Assert.Equal(0, payments.MergedRows);
        Assert.Equal(3, payments.Excluded["no_known_participant"]);
        Assert.Equal(3, payments.Quality["pairs_with_missing_profile"]);
        Assert.Equal(payments.Read, payments.AcceptedSourceRows + payments.Excluded.Values.Sum());
        Assert.False(await db.CompanyRows.AnyAsync(row => row.Id == "csv_payment_removed"));
        Assert.False(await db.PaymentRows.AnyAsync(row => row.Payer == "csv_payment_ghost_a" || row.Payer == "csv_payment_removed"));
        Assert.Equal(10, await db.PaymentRows.Where(row => row.Payer == "csv_payment_owner").SumAsync(row => row.PayAmt));
        Assert.Equal(90, await db.PaymentRows.Where(row => row.Beneficiary == "csv_payment_owner").SumAsync(row => row.PayAmt));
        Assert.True(await db.PaymentRows.AnyAsync(row => row.Payer == "csv_payment_outside" && row.Beneficiary == "test_company_a"));
        var report = await File.ReadAllTextAsync(package.ReportPath);
        await importer.ImportAsync(package.Job, package.ReportPath, false, CancellationToken.None);
        Assert.Equal(report, await File.ReadAllTextAsync(package.ReportPath));
    }

    [PostgresFact]
    public async Task AppliesPaymentFilterToLegacyImportOnce()
    {
        using var package = new TestPackage();
        await package.WriteAsync([Company("csv_orphan_upgrade")], [
            "csv_orphan_external;csv_orphan_upgrade;10;1", "csv_orphan_b;csv_orphan_a;30;3"
        ]);
        await using var db = fixture.CreateDb();
        var importer = new CsvImporter(db);
        await importer.ImportAsync(package.Job, package.ReportPath, false, CancellationToken.None);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO payment (payer, beneficiary, pay_amt, pay_cnt, dataset_version)
            VALUES ('csv_orphan_a', 'csv_orphan_b', 30, 3, {package.Job.DatasetVersion});
            UPDATE background_job SET stage = 'csv_imported_group_revenue_v1'
            WHERE kind = 'ImportCsv' AND payload->>'dataset_version' = {package.Job.DatasetVersion};
            """);
        var result = await importer.ImportAsync(package.Job, package.ReportPath, true, CancellationToken.None);
        Assert.False(result.Skipped);
        Assert.Equal(1, result.Report!.Payments.Excluded["no_known_participant"]);
        Assert.False(await db.PaymentRows.AnyAsync(row => row.Payer == "csv_orphan_a"));
        Assert.True(await db.PaymentRows.AnyAsync(row => row.Payer == "csv_orphan_upgrade" && row.PayAmt == 10));
        Assert.True((await importer.ImportAsync(package.Job, package.ReportPath, true, CancellationToken.None)).Skipped);
    }

    [PostgresFact]
    public async Task RejectsImportBeforeMigrations()
    {
        var database = "alfanews_unmigrated_" + Guid.NewGuid().ToString("N");
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString!) { Database = database }.ConnectionString;
        await using var admin = new NpgsqlConnection(fixture.ConnectionString);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE {database}", admin))
            await command.ExecuteNonQueryAsync();
        try
        {
            await using var db = new AlfaNewsDbContext(new DbContextOptionsBuilder<AlfaNewsDbContext>()
                .UseNpgsql(connectionString, options => options.UseVector()).Options);
            using var package = new TestPackage();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new CsvImporter(db)
                .ImportAsync(package.Job, package.ReportPath, true, CancellationToken.None));
            Assert.Contains("миграций", error.Message);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var command = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin);
            await command.ExecuteNonQueryAsync();
        }
    }

    [PostgresFact]
    public async Task ExcludesOverflowingGroupsAndKeepsOppositeDirections()
    {
        using var package = new TestPackage();
        await package.WriteAsync([Company("csv_overflow"), Company("csv_x")], [
            "csv_x;csv_y;1;9223372036854775807", "csv_x;csv_y;1;1",
            "csv_y;csv_x;3;1", "csv_m;csv_n;90000000000000000000000000;1",
            "csv_m;csv_n;90000000000000000000000000;1"
        ]);
        await using var db = fixture.CreateDb();
        var result = await new CsvImporter(db).ImportAsync(package.Job, package.ReportPath, false, CancellationToken.None);
        Assert.Equal(4, result.Report!.Payments.Excluded["aggregate_overflow"]);
        Assert.Equal(1, result.Report.Payments.Accepted);
        Assert.True(await db.PaymentRows.AnyAsync(row => row.Payer == "csv_x" && row.Beneficiary == "csv_y"));
    }

    private static string Company(string id, string revenue = "2000", string type = "CLIENT", string flag = "1.0", string okved = "01.11", string groupId = "")
    {
        var fields = Enumerable.Repeat("", 24).ToArray();
        fields[0] = id;
        fields[5] = okved;
        fields[6] = "Тестовая отрасль";
        fields[10] = type;
        fields[11] = flag;
        fields[20] = revenue;
        fields[21] = groupId;
        return string.Join(';', fields);
    }

    private sealed class TestPackage : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "alfanews_csv_" + Guid.NewGuid().ToString("N"));
        public ImportCsvJob Job { get; }
        public string ReportPath => Path.Combine(directory, "import_report.json");

        public TestPackage()
        {
            Directory.CreateDirectory(directory);
            Job = new ImportCsvJob(Path.Combine(directory, "company.csv"), Path.Combine(directory, "payment.csv"), Guid.NewGuid().ToString("N"));
        }

        public async Task WriteAsync(string[] companies, string[] payments)
        {
            await File.WriteAllTextAsync(Job.CompanyFile,
                string.Join(';', CsvImporter.CompanyHeaders) + "\n" + string.Join('\n', companies), new UTF8Encoding(false));
            await File.WriteAllTextAsync(Job.PaymentFile,
                "BENEFICIARY;PAYER;PAY_AMT;PAY_CNT\n" + string.Join('\n', payments), new UTF8Encoding(false));
        }

        public void Dispose() => Directory.Delete(directory, true);
    }
}