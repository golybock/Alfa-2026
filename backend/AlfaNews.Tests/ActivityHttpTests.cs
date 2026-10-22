using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using AlfaNews.Contracts;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Api.Access;

namespace AlfaNews.Tests;

public sealed class ActivityHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task EndpointEnforcesSessionOriginAccessAndStrictInput()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
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
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        using var client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}"),
            Timeout = TimeSpan.FromSeconds(10)
        };
        try
        {
            await WaitForApiAsync(client, process);
            var path = $"/api/insights/{fixture.InsightId}/activity";
            var options = new JsonSerializerOptions();
            ContractJson.Configure(options);
            var request = new UserActivityRequest(Guid.NewGuid().ToString(), UserActivityType.InsightOpened, null, null);
            var json = JsonSerializer.Serialize(request, options);
            Assert.Equal(HttpStatusCode.Unauthorized, await SendAsync(client, path, json, null, "https://bank.example"));
            Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(client, path, json, fixture.Token, "https://foreign.example"));
            Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(client, path, json, fixture.Token, null));
            Assert.Equal(HttpStatusCode.NoContent, await SendAsync(client, path, json, fixture.Token, "https://bank.example"));
            Assert.Equal(HttpStatusCode.NoContent, await SendAsync(client, path, json, fixture.Token, "https://bank.example"));
            Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(client, $"/api/insights/{fixture.ForeignInsightId}/activity", json, fixture.Token, "https://bank.example"));
            Assert.Equal(HttpStatusCode.BadRequest, await SendAsync(client, path, json.Replace("insight_opened", "chat_question_accepted"), fixture.Token, "https://bank.example"));
            Assert.Equal(HttpStatusCode.BadRequest, await SendAsync(client, path, json.Replace("\"action_id\":null", "\"action_id\":null,\"user_id\":\"forged\""), fixture.Token, "https://bank.example"));
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(true);
            await process.WaitForExitAsync();
            await output;
            await errors;
        }
    }

    private static async Task WaitForApiAsync(HttpClient client, Process process)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            Assert.False(process.HasExited, "API завершился до начала HTTP-проверки.");
            try
            {
                using var response = await client.GetAsync("/openapi/v1.json");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
            }
            await Task.Delay(100);
        }
        Assert.Fail("API не запустился за 10 секунд.");
    }

    private static async Task<HttpStatusCode> SendAsync(HttpClient client, string path, string json, string? token, string? origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        if (token is not null)
            request.Headers.Add("Cookie", $"{SessionAuthenticationHandler.CookieName}={token}");
        if (origin is not null)
            request.Headers.Add("Origin", origin);
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var options = new JsonSerializerOptions();
            ContractJson.Configure(options);
            var error = await response.Content.ReadFromJsonAsync<ApiError>(options);
            Assert.NotNull(error);
            Assert.NotEmpty(error.Code);
            Assert.NotEmpty(error.RequestId);
        }
        return response.StatusCode;
    }
}