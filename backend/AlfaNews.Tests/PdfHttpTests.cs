using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using AlfaNews.Api;
using AlfaNews.Contracts.Api;
using AlfaNews.Core.Access;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Tests;

public sealed class PdfHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task UploadEnforcesOperatorAndOriginAndReturnsSameDocumentForDuplicate()
    {
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        await using (var db = fixture.CreateDb())
        {
            var hash = SessionService.HashToken(token);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE auth_session SET token_hash = {hash} WHERE id = {fixture.OperatorSessionId}");
        }
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var root = Path.Combine(Path.GetTempPath(), "alfanews-http-pdf-" + Guid.NewGuid().ToString("N"));
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add("--runtimeconfig");
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "AlfaNews.Tests.runtimeconfig.json"));
        start.ArgumentList.Add("--depsfile");
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "AlfaNews.Tests.deps.json"));
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        start.Environment["ConnectionStrings__DefaultConnection"] = fixture.ConnectionString;
        start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["Auth__AllowedOrigins__0"] = "https://bank.example";
        start.Environment["Storage__RootPath"] = root;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        try
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                Assert.False(process.HasExited);
                try
                {
                    if ((await client.GetAsync("/openapi/v1.json")).IsSuccessStatusCode)
                        break;
                }
                catch (HttpRequestException) { }
                await Task.Delay(100);
            }
            var bytes = PdfProcessingTests.CreatePdf(1);
            Assert.Equal(HttpStatusCode.Unauthorized, (await UploadAsync(client, bytes, null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(client, bytes, fixture.Token)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(client, bytes, token, "https://foreign.example")).StatusCode);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await UploadAsync(client, "invalid"u8.ToArray(), token)).StatusCode);
            using var first = await UploadAsync(client, bytes, token);
            Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
            var accepted = await first.Content.ReadFromJsonAsync<DocumentAcceptedResponse>(AlfaNews.Core.Processing.JobKinds.JsonOptions);
            using var second = await UploadAsync(client, bytes, token);
            Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
            Assert.Equal(accepted, await second.Content.ReadFromJsonAsync<DocumentAcceptedResponse>(AlfaNews.Core.Processing.JobKinds.JsonOptions));
            using var status = new HttpRequestMessage(HttpMethod.Get, $"/api/jobs/{accepted!.JobId}");
            status.Headers.Add("Cookie", $"alfa_session={token}");
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(status)).StatusCode);
            await using var db = fixture.CreateDb();
            var id = Guid.Parse(accepted.DocumentId);
            Assert.Equal("queued", (await db.ResearchDocRows.SingleAsync(row => row.Id == id)).OcrStatus);
            Assert.Single(Directory.GetFiles(Path.Combine(root, "files")));
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(true);
            await process.WaitForExitAsync();
            await output;
            await error;
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] bytes, string? token, string origin = "https://bank.example")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/documents");
        request.Headers.Add("Origin", origin);
        if (token is not null)
            request.Headers.Add("Cookie", $"alfa_session={token}");
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", "../../forged.pdf");
        content.Add(new StringContent("Тест загрузки"), "title");
        request.Content = content;
        return client.SendAsync(request);
    }
}