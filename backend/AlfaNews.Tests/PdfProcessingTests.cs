using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Access;
using AlfaNews.Core.Data;
using AlfaNews.Core.Documents;
using AlfaNews.Core.Llm;
using AlfaNews.Core.Processing;
using AlfaNews.Worker;
using AlfaNews.Worker.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlfaNews.Tests;

public sealed class PdfProcessingTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public void RejectsPathsOutsideStorage()
    {
        var options = new PdfOptions { RootPath = Path.GetTempPath() };
        Assert.Equal(Path.Combine(Path.GetTempPath(), "inside.pdf"), options.Resolve("inside.pdf"));
        Assert.Throws<InvalidOperationException>(() => options.Resolve("../outside.pdf"));
        Assert.Throws<InvalidOperationException>(() => options.Resolve(Path.GetPathRoot(Path.GetTempPath())!));
    }

    [PostgresFact]
    public async Task RegistersConcurrentDuplicatesOnceAndScannerDoesNotRepeatCompletedOcr()
    {
        var root = Path.Combine(Path.GetTempPath(), "alfanews-pdf-" + Guid.NewGuid().ToString("N"));
        var options = new PdfOptions { RootPath = root, InputPath = Path.Combine(root, "input") };
        var client = new PageClient();
        await using var services = Services(options, client);
        try
        {
            var bytes = CreatePdf(2);
            var registrations = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
            {
                await using var scope = services.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<PdfRegistration>().RegisterAsync(new MemoryStream(bytes), "Тест", null, null, null, CancellationToken.None);
            }));
            Assert.Equal(registrations[0], registrations[1]);
            var dispatcher = services.GetRequiredService<JobDispatcher>();
            while (await dispatcher.DispatchNextAsync(CancellationToken.None)) { }
            await using var db = fixture.CreateDb();
            var id = Guid.Parse(registrations[0].DocumentId);
            var document = await db.ResearchDocRows.SingleAsync(row => row.Id == id);
            Assert.Equal("ready", document.OcrStatus);
            Assert.Equal("not_reviewed", document.ReviewStatus);
            Assert.Equal(2, await db.ResearchPageRows.CountAsync(row => row.DocumentId == id && row.OcrStatus == "ready"));
            var initialCalls = client.Calls;
            var initialJobs = await db.BackgroundJobRows.CountAsync();
            Directory.CreateDirectory(options.InputPath);
            var path = Path.Combine(options.InputPath, "duplicate.PDF");
            await File.WriteAllBytesAsync(path, bytes);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-1));
            File.Delete(options.Resolve(document.FilePath));
            await using var scanScope = services.CreateAsyncScope();
            var scan = scanScope.ServiceProvider.GetRequiredService<PdfScan>();
            await scan.ScanAsync(CancellationToken.None);
            await scan.ScanAsync(CancellationToken.None);
            Assert.Equal(initialJobs, await db.BackgroundJobRows.CountAsync());
            Assert.Equal(initialCalls, client.Calls);
            Assert.False(await dispatcher.DispatchNextAsync(CancellationToken.None));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(options.Resolve(document.FilePath)));
            var newPath = Path.Combine(options.InputPath, "new.PDF");
            await File.WriteAllBytesAsync(newPath, CreatePdf(1));
            File.SetLastWriteTimeUtc(newPath, DateTime.UtcNow.AddMinutes(-1));
            await scan.ScanAsync(CancellationToken.None);
            await scan.ScanAsync(CancellationToken.None);
            Assert.Equal(initialJobs + 1, await db.BackgroundJobRows.CountAsync());
            while (await dispatcher.DispatchNextAsync(CancellationToken.None)) { }
            Assert.Equal(initialCalls + 1, client.Calls);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [PostgresFact]
    public async Task PreservesSuccessfulPageAndFinalizesAfterExplicitRetry()
    {
        var root = Path.Combine(Path.GetTempPath(), "alfanews-pdf-" + Guid.NewGuid().ToString("N"));
        var options = new PdfOptions { RootPath = root };
        var client = new PageClient { FailPageTwo = true };
        await using var services = Services(options, client);
        try
        {
            await using var scope = services.CreateAsyncScope();
            var response = await scope.ServiceProvider.GetRequiredService<PdfRegistration>()
                .RegisterAsync(new MemoryStream(CreatePdf(2)), "Частичный OCR", null, null, null, CancellationToken.None);
            var dispatcher = services.GetRequiredService<JobDispatcher>();
            while (await dispatcher.DispatchNextAsync(CancellationToken.None)) { }
            await using var db = fixture.CreateDb();
            var id = Guid.Parse(response.DocumentId);
            Assert.Equal("partial", (await db.ResearchDocRows.SingleAsync(row => row.Id == id)).OcrStatus);
            var pages = await db.ResearchPageRows.Where(row => row.DocumentId == id).OrderBy(row => row.PageNo).ToArrayAsync();
            Assert.Equal("ready", pages[0].OcrStatus);
            Assert.Equal("failed", pages[1].OcrStatus);
            Assert.Equal("test_failure", pages[1].LastError);
            var successfulJson = pages[0].OcrJson!.Value.GetRawText();
            var failed = await db.BackgroundJobRows.SingleAsync(row => row.Kind == "OcrPdfPage" && row.Status == "failed");
            client.FailPageTwo = false;
            Assert.True(await scope.ServiceProvider.GetRequiredService<JobQueue>().RetryAsync(failed.Id, CancellationToken.None));
            while (await dispatcher.DispatchNextAsync(CancellationToken.None)) { }
            db.ChangeTracker.Clear();
            Assert.Equal("ready", (await db.ResearchDocRows.SingleAsync(row => row.Id == id)).OcrStatus);
            Assert.Equal(successfulJson, (await db.ResearchPageRows.SingleAsync(row => row.Id == pages[0].Id)).OcrJson!.Value.GetRawText());
            Assert.Equal(3, client.Calls);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [PostgresFact]
    public async Task RejectsInvalidSignatureAndPageLimitWithoutRegistering()
    {
        var root = Path.Combine(Path.GetTempPath(), "alfanews-pdf-" + Guid.NewGuid().ToString("N"));
        await using var services = Services(new PdfOptions { RootPath = root }, new PageClient());
        try
        {
            await using var scope = services.CreateAsyncScope();
            var registration = scope.ServiceProvider.GetRequiredService<PdfRegistration>();
            Assert.Equal(415, (await Assert.ThrowsAsync<AccessException>(() => registration.RegisterAsync(new MemoryStream("bad"u8.ToArray()), "Тест", null, null, null, CancellationToken.None))).StatusCode);
            Assert.Equal(400, (await Assert.ThrowsAsync<AccessException>(() => registration.RegisterAsync(new MemoryStream(CreatePdf(51)), "Тест", null, null, null, CancellationToken.None))).StatusCode);
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "files")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private ServiceProvider Services(PdfOptions options, PageClient client)
    {
        var services = new ServiceCollection();
        services.AddAlfaNewsData(fixture.ConnectionString!);
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(options);
        services.AddAlfaNewsLlm(new LlmOptions());
        services.AddSingleton<ILlmClient>(client);
        services.AddScoped<PdfRegistration>();
        services.AddScoped<PdfTools>();
        services.AddScoped<PdfProcessing>();
        services.AddScoped<PdfScan>();
        services.AddScoped<JobQueue>();
        services.AddSingleton<JobDispatcher>();
        services.AddScoped<IJobHandler, IndexPdfJobHandler>();
        services.AddScoped<IJobHandler, OcrPdfPageJobHandler>();
        services.AddScoped<IJobHandler, FinalizePdfJobHandler>();
        return services.BuildServiceProvider();
    }

    internal static byte[] CreatePdf(int pages)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(' ', Enumerable.Range(0, pages).Select(index => $"{index * 2 + 3} 0 R"))}] /Count {pages} >>"
        };
        for (var index = 0; index < pages; index++)
        {
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Contents {index * 2 + 4} 0 R >>");
            var content = $"0.5 g 20 20 {RandomNumberGenerator.GetInt32(50, 250)} 100 re f";
            objects.Add($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
        }
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        var xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private sealed class PageClient : ILlmClient
    {
        public int Calls { get; private set; }
        public bool FailPageTwo { get; set; }

        public Task<LlmClientResponse> SendAsync(LlmPrompt prompt, LlmRoute route, LlmImage? image, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.NotNull(image);
            Assert.Equal("low", route.Reasoning);
            if (FailPageTwo && prompt.User.Contains("\"page_no\":2", StringComparison.Ordinal))
                return Task.FromResult(new LlmClientResponse(Error: "test_failure", ActualCostRub: 0));
            var text = JsonSerializer.SerializeToElement(new
            {
                blocks = new[] { new { local_id = "b001", kind = "fact", text = "Синтетический текст",
                tables = Array.Empty<object>(), charts = Array.Empty<object>(), tags = new { factors = Array.Empty<string>(), okved2 = Array.Empty<string>(),
                    countries = Array.Empty<string>(), regions = Array.Empty<string>(), commodities = Array.Empty<string>() } } }
            });
            return Task.FromResult(new LlmClientResponse(text.GetRawText(), ActualCostRub: 0, FinishReason: "stop"));
        }
    }
}