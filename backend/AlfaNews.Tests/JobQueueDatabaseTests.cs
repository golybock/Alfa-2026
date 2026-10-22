using System.Diagnostics;
using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Processing;
using AlfaNews.Core.Importing;
using AlfaNews.Core.Profiles;
using Microsoft.Extensions.Configuration;
using AlfaNews.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;

namespace AlfaNews.Tests;

public sealed class JobQueueDatabaseTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task DeduplicatesTypedPayloadWithinOwnerAndCompany()
    {
        await ResetAsync();
        await using var db = fixture.CreateDb();
        var queue = new JobQueue(db, TimeProvider.System);
        var payload = new ParseNewsJob(Guid.NewGuid().ToString());
        var first = await queue.EnqueueAsync(payload, fixture.UserId, "test_company_a", CancellationToken.None);
        var duplicate = await queue.EnqueueAsync(payload, fixture.UserId, "test_company_a", CancellationToken.None);
        var otherOwner = await queue.EnqueueAsync(payload, fixture.OtherUserId, "test_company_a", CancellationToken.None);
        var otherCompany = await queue.EnqueueAsync(payload, fixture.UserId, "test_company_b", CancellationToken.None);
        Assert.Equal(first.Id, duplicate.Id);
        Assert.NotEqual(first.Id, otherOwner.Id);
        Assert.NotEqual(first.Id, otherCompany.Id);
        Assert.Equal(payload, first.Payload.Deserialize<ParseNewsJob>(JobKinds.JsonOptions));
        Assert.Equal(first.Id, (await queue.FindActiveAsync(payload, fixture.UserId, "test_company_a", CancellationToken.None))!.Id);
    }

    [PostgresFact]
    public async Task SelectsPriorityThenTimeAndContinuesAfterFailureAndMissingHandler()
    {
        await ResetAsync();
        var order = new List<string>();
        await using var services = CreateServices(
            new TestHandler("CollectNews", (_, _) => throw new InvalidOperationException("private/path secret")),
            new TestHandler("ParseNews", (job, _) =>
            {
                order.Add(job.Payload.Deserialize<ParseNewsJob>(JobKinds.JsonOptions)!.NewsRawId);
                return Task.FromResult(new JobResult());
            }),
            new TestHandler("OcrPdfPage", (_, _) =>
            {
                order.Add("ocr");
                return Task.FromResult(new JobResult());
            }));
        await using var db = fixture.CreateDb();
        var queue = new JobQueue(db, TimeProvider.System);
        var ocr = await queue.EnqueueAsync(new OcrPdfPageJob("document", 1), null, null, CancellationToken.None);
        var older = await queue.EnqueueAsync(new ParseNewsJob("older"), null, null, CancellationToken.None);
        var newer = await queue.EnqueueAsync(new ParseNewsJob("newer"), null, null, CancellationToken.None);
        var failure = await queue.EnqueueAsync(new CollectNewsJob(["test"]), null, null, CancellationToken.None);
        var missing = await queue.EnqueueAsync(new IndexPdfJob("document"), null, null, CancellationToken.None);
        older.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        newer.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        var dispatcher = services.GetRequiredService<JobDispatcher>();
        for (var i = 0; i < 5; i++)
            Assert.True(await dispatcher.DispatchNextAsync(CancellationToken.None));
        Assert.False(await dispatcher.DispatchNextAsync(CancellationToken.None));
        Assert.Equal(["older", "newer", "ocr"], order);
        await db.Entry(failure).ReloadAsync();
        await db.Entry(missing).ReloadAsync();
        await db.Entry(ocr).ReloadAsync();
        Assert.Equal("failed", failure.Status);
        Assert.Equal("job_failed", failure.LastError);
        Assert.Equal("not_implemented", missing.LastError);
        Assert.Equal("done", ocr.Status);
        Assert.Equal(1, ocr.Attempts);
        Assert.NotNull(ocr.StartedAt);
        Assert.NotNull(ocr.FinishedAt);
    }

    [PostgresFact]
    public async Task ShutdownLeavesRunningAndRestartAutomaticallyRequeuesIt()
    {
        await ResetAsync();
        using var stopping = new CancellationTokenSource();
        await using var services = CreateServices(new TestHandler("ParseNews", (_, token) =>
        {
            stopping.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(new JobResult());
        }));
        Guid id;
        await using (var db = fixture.CreateDb())
            id = (await new JobQueue(db, TimeProvider.System).EnqueueAsync(new ParseNewsJob("restart"), null, null, CancellationToken.None)).Id;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => services.GetRequiredService<JobDispatcher>().DispatchNextAsync(stopping.Token));
        await using (var db = fixture.CreateDb())
        {
            var job = await db.BackgroundJobRows.SingleAsync(row => row.Id == id);
            Assert.Equal("running", job.Status);
            Assert.Equal(1, job.Attempts);
            Assert.Null(job.FinishedAt);
            Assert.Equal(1, await new JobQueue(db, TimeProvider.System).RecoverAsync(CancellationToken.None));
        }
        await using var restarted = CreateServices(new TestHandler("ParseNews", (_, _) => Task.FromResult(new JobResult())));
        Assert.True(await restarted.GetRequiredService<JobDispatcher>().DispatchNextAsync(CancellationToken.None));
        await using var resultDb = fixture.CreateDb();
        var result = await resultDb.BackgroundJobRows.SingleAsync(row => row.Id == id);
        Assert.Equal("done", result.Status);
        Assert.Equal(2, result.Attempts);
    }

    [PostgresFact]
    public async Task RecoveryDoesNotRestartKeyBlockedJobsAndManualRetryKeepsAttempts()
    {
        await ResetAsync();
        await using var db = fixture.CreateDb();
        var queue = new JobQueue(db, TimeProvider.System);
        var job = await queue.EnqueueAsync(new ParseNewsJob("blocked"), null, null, CancellationToken.None);
        job.Status = "failed";
        job.Stage = "failed";
        job.LastError = "llm_key_required";
        job.Attempts = 2;
        job.FinishedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        Assert.Equal(0, await queue.RecoverAsync(CancellationToken.None));
        Assert.True(await queue.RetryAsync(job.Id, CancellationToken.None));
        await db.Entry(job).ReloadAsync();
        Assert.Equal("queued", job.Status);
        Assert.Null(job.LastError);
        Assert.Null(job.FinishedAt);
        Assert.Equal(2, job.Attempts);
        Assert.False(await queue.RetryAsync(job.Id, CancellationToken.None));
        Assert.False(await queue.RetryAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [PostgresFact]
    public async Task ScheduleDoesNotAccumulateActiveRefreshesWhenAsOfChanges()
    {
        await ResetAsync();
        await using var db = fixture.CreateDb();
        await db.SourceRows.Where(row => row.Id == "synthetic").ExecuteUpdateAsync(update => update.SetProperty(row => row.Enabled, true));
        var clock = new TestClock();
        var queue = new JobQueue(db, clock);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Feed:CompanyIds"] = "test_company_a,test_company_b"
        }).Build();
        var schedule = new JobSchedule(db, queue, clock, configuration);
        var companyCount = 2;
        await schedule.EnqueueAsync(CancellationToken.None);
        clock.Now = clock.Now.AddMinutes(2);
        await schedule.EnqueueAsync(CancellationToken.None);
        Assert.Equal(1, await db.BackgroundJobRows.CountAsync(row => row.Kind == "CollectNews"));
        Assert.Equal(companyCount, await db.BackgroundJobRows.CountAsync(row => row.Kind == "RefreshFeed"));
        await db.BackgroundJobRows.Where(row => row.Kind == "RefreshFeed" && row.CompanyId == "test_company_a")
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, "done"));
        await schedule.EnqueueAsync(CancellationToken.None);
        Assert.Equal(companyCount + 1, await db.BackgroundJobRows.CountAsync(row => row.Kind == "RefreshFeed"));
    }

    [PostgresFact]
    public async Task ScheduleRefreshesOnlyListedCompaniesAndIgnoresDuplicateAndUnknownIds()
    {
        await ResetAsync();
        await using var db = fixture.CreateDb();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Feed:CompanyIds"] = " test_company_a, test_company_a,missing_company, "
        }).Build();
        var queue = new JobQueue(db, TimeProvider.System);
        await new JobSchedule(db, queue, TimeProvider.System, configuration).EnqueueAsync(CancellationToken.None);
        var job = Assert.Single(await db.BackgroundJobRows.Where(row => row.Kind == "RefreshFeed").ToListAsync());
        Assert.Equal("test_company_a", job.CompanyId);
        Assert.Equal("test_company_a", job.Payload.Deserialize<RefreshFeedJob>(JobKinds.JsonOptions)!.CompanyId);
    }

    [PostgresFact]
    public async Task EmptyCompanyListDisablesRefreshWithoutDisablingCollection()
    {
        await ResetAsync();
        await using var db = fixture.CreateDb();
        await db.SourceRows.Where(row => row.Id == "synthetic")
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Enabled, true));
        var queue = new JobQueue(db, TimeProvider.System);
        foreach (var companyIds in new string?[] { null, "", " , , " })
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Feed:CompanyIds"] = companyIds
            }).Build();
            await new JobSchedule(db, queue, TimeProvider.System, configuration).EnqueueAsync(CancellationToken.None);
        }
        Assert.False(await db.BackgroundJobRows.AnyAsync(row => row.Kind == "RefreshFeed"));
        Assert.Equal(1, await db.BackgroundJobRows.CountAsync(row => row.Kind == "CollectNews"));
    }

    [PostgresFact]
    public async Task InvalidPayloadFailsWithoutStoppingFollowingJob()
    {
        await ResetAsync();
        await using var db = fixture.CreateDb();
        var queue = new JobQueue(db, TimeProvider.System);
        var invalid = await queue.EnqueueAsync(new ParseNewsJob("invalid"), null, null, CancellationToken.None);
        invalid.Payload = JsonSerializer.SerializeToElement(new { unknown = "private" });
        await db.SaveChangesAsync();
        var next = await queue.EnqueueAsync(new ParseNewsJob("valid"), null, null, CancellationToken.None);
        await using var services = CreateServices(new TestHandler("ParseNews", (_, _) => Task.FromResult(new JobResult())));
        var dispatcher = services.GetRequiredService<JobDispatcher>();
        Assert.True(await dispatcher.DispatchNextAsync(CancellationToken.None));
        Assert.True(await dispatcher.DispatchNextAsync(CancellationToken.None));
        await db.Entry(invalid).ReloadAsync();
        await db.Entry(next).ReloadAsync();
        Assert.Equal("invalid_payload", invalid.LastError);
        Assert.Equal("done", next.Status);
    }

    [PostgresFact]
    public async Task RealImportAndProfileHandlersCompleteOriginalJobsWithoutExtraRows()
    {
        await ResetAsync();
        var directory = Path.Combine(Path.GetTempPath(), "alfanews_queue_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var import = new ImportCsvJob(Path.Combine(directory, "company.csv"), Path.Combine(directory, "payment.csv"), Guid.NewGuid().ToString("N"));
            var fields = Enumerable.Repeat("", CsvImporter.CompanyHeaders.Length).ToArray();
            fields[0] = "queued_company";
            fields[5] = "01.11";
            fields[10] = "CLIENT";
            fields[20] = "2000";
            await File.WriteAllTextAsync(import.CompanyFile, string.Join(';', CsvImporter.CompanyHeaders) + "\n" + string.Join(';', fields));
            await File.WriteAllTextAsync(import.PaymentFile, "BENEFICIARY;PAYER;PAY_AMT;PAY_CNT");
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Data:ImportReportPath"] = Path.Combine(directory, "import.json"),
                ["Data:ProfilesReportPath"] = Path.Combine(directory, "profiles.json")
            }).Build();
            var registrations = new ServiceCollection();
            registrations.AddLogging();
            registrations.AddSingleton<IConfiguration>(configuration);
            registrations.AddSingleton(TimeProvider.System);
            registrations.AddDbContext<AlfaNewsDbContext>(options => options.UseNpgsql(fixture.ConnectionString!, postgres => postgres.UseVector()));
            registrations.AddScoped<CsvImporter>();
            registrations.AddScoped<CsvImportService>();
            registrations.AddScoped<ProfileBuilder>();
            registrations.AddScoped<ProfileBuildService>();
            registrations.AddScoped<IJobHandler, ImportCsvJobHandler>();
            registrations.AddScoped<IJobHandler, RebuildProfilesJobHandler>();
            registrations.AddSingleton<JobDispatcher>();
            await using var services = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            await using var db = fixture.CreateDb();
            var queue = new JobQueue(db, TimeProvider.System);
            var importRow = await queue.EnqueueAsync(import, null, null, CancellationToken.None);
            var profilesRow = await queue.EnqueueAsync(new RebuildProfilesJob(import.DatasetVersion), null, null, CancellationToken.None);
            var dispatcher = services.GetRequiredService<JobDispatcher>();
            Assert.True(await dispatcher.DispatchNextAsync(CancellationToken.None));
            Assert.True(await dispatcher.DispatchNextAsync(CancellationToken.None));
            await db.Entry(importRow).ReloadAsync();
            await db.Entry(profilesRow).ReloadAsync();
            Assert.Equal("done", importRow.Status);
            Assert.Equal("csv_package", importRow.ResultType);
            Assert.Equal("done", profilesRow.Status);
            Assert.Equal("profile_version", profilesRow.ResultType);
            Assert.Equal(2, await db.BackgroundJobRows.CountAsync());
            Assert.True(File.Exists(configuration["Data:ImportReportPath"]));
            Assert.True(File.Exists(configuration["Data:ProfilesReportPath"]));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [PostgresFact]
    public async Task HandlerCanReportKeyRequiredWithoutAutomaticRetry()
    {
        await ResetAsync();
        await using var db = fixture.CreateDb();
        var queue = new JobQueue(db, TimeProvider.System);
        var job = await queue.EnqueueAsync(new ParseNewsJob("key-required"), null, null, CancellationToken.None);
        await using var services = CreateServices(new TestHandler("ParseNews", (_, _) => Task.FromResult(new JobResult(ErrorCode: "llm_key_required"))));
        Assert.True(await services.GetRequiredService<JobDispatcher>().DispatchNextAsync(CancellationToken.None));
        await db.Entry(job).ReloadAsync();
        Assert.Equal("failed", job.Status);
        Assert.Equal("llm_key_required", JobQueue.ToResponse(job, "r").Error!.Code);
        Assert.Equal(0, await queue.RecoverAsync(CancellationToken.None));
        Assert.False(await services.GetRequiredService<JobDispatcher>().DispatchNextAsync(CancellationToken.None));
    }

    [PostgresFact]
    public async Task WorkerProcessRecoversRunningJobAndRetryCommandRequeuesFailure()
    {
        await ResetAsync();
        Guid id;
        await using (var db = fixture.CreateDb())
        {
            var job = await new JobQueue(db, TimeProvider.System).EnqueueAsync(new ParseNewsJob("process-restart"), null, null, CancellationToken.None);
            id = job.Id;
            job.Status = "running";
            job.Attempts = 1;
            job.StartedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
        using (var process = StartWorker("run-worker"))
        {
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                while (true)
                {
                    await using var db = fixture.CreateDb();
                    var job = await db.BackgroundJobRows.AsNoTracking().SingleAsync(row => row.Id == id, timeout.Token);
                    if (job.Status == "failed")
                    {
                        Assert.Equal("not_implemented", job.LastError);
                        Assert.Equal(2, job.Attempts);
                        break;
                    }
                    if (process.HasExited)
                    {
                        var diagnostics = (await output) + (await error);
                        diagnostics = diagnostics.Replace(fixture.ConnectionString!, "[connection]");
                        var password = new Npgsql.NpgsqlConnectionStringBuilder(fixture.ConnectionString!).Password;
                        if (!string.IsNullOrEmpty(password))
                            diagnostics = diagnostics.Replace(password, "[password]");
                        Assert.Fail("Worker завершился до обработки задания: " + diagnostics);
                    }
                    await Task.Delay(50, timeout.Token);
                }
            }
            finally
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                await output;
                await error;
            }
        }
        using var retry = StartWorker("retry-job", id.ToString());
        var retryOutput = retry.StandardOutput.ReadToEndAsync();
        var retryError = retry.StandardError.ReadToEndAsync();
        using var retryTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await retry.WaitForExitAsync(retryTimeout.Token);
            Assert.Equal(0, retry.ExitCode);
            await using var db = fixture.CreateDb();
            var job = await db.BackgroundJobRows.AsNoTracking().SingleAsync(row => row.Id == id);
            Assert.Equal("queued", job.Status);
            Assert.Null(job.LastError);
            Assert.Equal(2, job.Attempts);
        }
        finally
        {
            if (!retry.HasExited)
                retry.Kill(entireProcessTree: true);
            await retry.WaitForExitAsync();
            await retryOutput;
            await retryError;
        }
    }

    private Process StartWorker(params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add("--runtimeconfig");
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "AlfaNews.Tests.runtimeconfig.json"));
        start.ArgumentList.Add("--depsfile");
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "AlfaNews.Tests.deps.json"));
        start.ArgumentList.Add(typeof(ImportCsvJobHandler).Assembly.Location);
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        start.Environment["ConnectionStrings__DefaultConnection"] = fixture.ConnectionString!;
        start.Environment["Data__ImportOnStartup"] = "false";
        start.Environment["Data__BuildProfilesOnStartup"] = "false";
        return Process.Start(start)!;
    }

    private async Task ResetAsync()
    {
        await using var db = fixture.CreateDb();
        await db.BackgroundJobRows.ExecuteDeleteAsync();
    }

    private ServiceProvider CreateServices(params IJobHandler[] handlers)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<AlfaNewsDbContext>(options => options.UseNpgsql(fixture.ConnectionString!, postgres => postgres.UseVector()));
        foreach (var handler in handlers)
            services.AddScoped<IJobHandler>(_ => handler);
        services.AddSingleton<JobDispatcher>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed class TestHandler(string kind, Func<BackgroundJobEntity, CancellationToken, Task<JobResult>> handle) : IJobHandler
    {
        public string Kind => kind;
        public Task<JobResult> HandleAsync(BackgroundJobEntity job, CancellationToken cancellationToken) => handle(job, cancellationToken);
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}