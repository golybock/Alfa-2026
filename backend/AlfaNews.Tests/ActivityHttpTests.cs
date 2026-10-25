using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using AlfaNews.Api;
using AlfaNews.Contracts;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Api.Access;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Tests;

public sealed class ActivityHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Origin = "https://bank.example";

    [PostgresFact]
    public Task EndpointEnforcesSessionOriginAccessAndStrictInput() => RunApiAsync(async client =>
    {
        var path = $"/api/insights/{fixture.InsightId}/activity";
        var options = new JsonSerializerOptions();
        ContractJson.Configure(options);
        var request = new UserActivityRequest(Guid.NewGuid().ToString(), UserActivityType.InsightOpened, null, null);
        var json = JsonSerializer.Serialize(request, options);
        Assert.Equal(HttpStatusCode.Unauthorized, await SendAsync(client, path, json, null, Origin));
        Assert.Equal(HttpStatusCode.Forbidden,
            await SendAsync(client, path, json, fixture.Token, "https://foreign.example"));
        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(client, path, json, fixture.Token, null));
        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(client, path, json, fixture.Token, Origin));
        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(client, path, json, fixture.Token, Origin));
        Assert.Equal(HttpStatusCode.Forbidden,
            await SendAsync(client, $"/api/insights/{fixture.ForeignInsightId}/activity", json, fixture.Token, Origin));
        Assert.Equal(HttpStatusCode.BadRequest,
            await SendAsync(client, path, json.Replace("insight_opened", "chat_question_accepted"), fixture.Token,
                Origin));
        Assert.Equal(HttpStatusCode.BadRequest,
            await SendAsync(client, path,
                json.Replace("\"action_id\":null", "\"action_id\":null,\"user_id\":\"forged\""), fixture.Token,
                Origin));
    });

    [PostgresFact]
    public Task ChatFeedbackAndEvidenceFollowContract() => RunApiAsync(async client =>
    {
        var chat = $"/api/insights/{fixture.InsightId}/chat";
        Assert.Equal(HttpStatusCode.BadRequest, await SendAsync(client, chat, Question("   "), fixture.Token, Origin));
        Assert.Equal(HttpStatusCode.BadRequest,
            await SendAsync(client, chat, Question(new string('я', 1001)), fixture.Token, Origin));
        Assert.Equal(HttpStatusCode.Forbidden,
            await SendAsync(client, $"/api/insights/{fixture.ForeignInsightId}/chat", Question("Почему?"),
                fixture.Token, Origin));
        var first = await PostJsonAsync(client, chat, Question(" Почему это важно? "), fixture.Token);
        Assert.Equal(HttpStatusCode.Accepted, first.Status);
        var again = await PostJsonAsync(client, chat, Question("Почему это важно?"), fixture.Token);
        Assert.Equal(HttpStatusCode.Accepted, again.Status);
        Assert.Equal(first.Body.GetProperty("job_id").GetString(), again.Body.GetProperty("job_id").GetString());
        Assert.Equal(HttpStatusCode.TooManyRequests,
            await SendAsync(client, chat, Question("Другой вопрос"), fixture.Token, Origin));
        await FailQuestionsAsync();
        await using (var db = fixture.CreateDb())
            Assert.Equal(1,
                await db.UserActivityRows.CountAsync(row =>
                    row.UserId == fixture.UserId && row.Kind == "chat_question_accepted"));
        Assert.Equal(HttpStatusCode.Accepted,
            await SendAsync(client, chat, Question("Почему это важно?"), fixture.Token, Origin));
        await RefuseQuestionsAsync();
        for (var question = 2; question <= 10; question++)
        {
            Assert.Equal(HttpStatusCode.Accepted,
                await SendAsync(client, chat, Question($"Вопрос {question}"), fixture.Token, Origin));
            await RefuseQuestionsAsync();
        }

        Assert.Equal(HttpStatusCode.TooManyRequests,
            await SendAsync(client, chat, Question("Одиннадцатый"), fixture.Token, Origin));
        var history = await GetJsonAsync(client, chat, fixture.Token);
        Assert.Equal(10, history.GetProperty("items").GetArrayLength());
        Assert.All(history.GetProperty("items").EnumerateArray(), item =>
        {
            Assert.Equal("refusal", item.GetProperty("answer").GetProperty("status").GetString());
            Assert.Equal(0, item.GetProperty("answer").GetProperty("questions_remaining").GetInt32());
        });
        Assert.Equal(0, (await GetJsonAsync(client, chat, fixture.OtherToken)).GetProperty("items").GetArrayLength());

        await SetInsightStatusAsync("processing");
        Assert.Equal(HttpStatusCode.Conflict,
            await SendAsync(client, chat, Question("Почему?"), fixture.OtherToken, Origin));
        await SetInsightStatusAsync("ready");

        var evidence = await GetJsonAsync(client, $"/api/insights/{fixture.InsightId}/evidence/E1", fixture.Token);
        Assert.Equal("news", evidence.GetProperty("type").GetString());
        Assert.Equal(HttpStatusCode.NotFound,
            await GetAsync(client, $"/api/insights/{fixture.InsightId}/evidence/E99", fixture.Token));
        Assert.Equal(HttpStatusCode.Forbidden,
            await GetAsync(client, $"/api/insights/{fixture.ForeignInsightId}/evidence/E1", fixture.Token));

        var feedback = $"/api/insights/{fixture.InsightId}/feedback";
        Assert.Equal(HttpStatusCode.BadRequest,
            await SendAsync(client, feedback, """{"kind":"like","reason":"obvious"}""", fixture.Token, Origin));
        Assert.Equal(HttpStatusCode.BadRequest,
            await SendAsync(client, feedback, """{"kind":"star","reason":null}""", fixture.Token, Origin));
        Assert.Equal(HttpStatusCode.Forbidden,
            await SendAsync(client, $"/api/insights/{fixture.ForeignInsightId}/feedback",
                """{"kind":"hide","reason":null}""", fixture.Token, Origin));
        Assert.Equal(HttpStatusCode.NoContent,
            await SendAsync(client, feedback, """{"kind":"dislike","reason":"obvious"}""", fixture.Token, Origin));
        Assert.Equal(HttpStatusCode.NoContent,
            await SendAsync(client, feedback, """{"kind":"hide","reason":"not_relevant"}""", fixture.Token, Origin));
        await using (var db = fixture.CreateDb())
        {
            var row = await db.FeedbackRows.SingleAsync(item => item.UserId == fixture.UserId);
            Assert.Equal(("hide", "not_relevant", "test_company_a"), (row.Kind, row.Reason, row.CompanyId));
        }
    });

    private static string Question(string text) => JsonSerializer.Serialize(new { question = text });

    // Без Worker вопрос остаётся в очереди. Сбой освобождает место и не засчитывается в лимит, отказ засчитывается.
    private async Task FailQuestionsAsync()
    {
        await using var db = fixture.CreateDb();
        await db.ChatMessageRows.Where(row => row.UserId == fixture.UserId && row.Status == "queued")
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, "failed"));
    }

    private async Task RefuseQuestionsAsync()
    {
        await using var db = fixture.CreateDb();
        await db.Database.ExecuteSqlInterpolatedAsync($$"""
                                                        UPDATE chat_message SET status = 'ready', answer_json = jsonb_build_object('id', id::text, 'insight_id', insight_id::text,
                                                            'status', 'refusal', 'answer', null, 'claims', '[]'::jsonb, 'evidence', '{}'::jsonb, 'missing', '[]'::jsonb,
                                                            'refusal_reason', 'Отказ.', 'suggested_questions', '[]'::jsonb, 'questions_remaining', 0)
                                                        WHERE user_id = {{fixture.UserId}} AND status = 'queued'
                                                        """);
    }

    private async Task SetInsightStatusAsync(string status)
    {
        await using var db = fixture.CreateDb();
        await db.InsightRows.Where(row => row.Id == fixture.InsightId)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, status));
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> PostJsonAsync(HttpClient client, string path,
        string json, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        request.Headers.Add("Cookie", $"{SessionAuthenticationHandler.CookieName}={token}");
        request.Headers.Add("Origin", Origin);
        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>());
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"{SessionAuthenticationHandler.CookieName}={token}");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<HttpStatusCode> GetAsync(HttpClient client, string path, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"{SessionAuthenticationHandler.CookieName}={token}");
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private async Task RunApiAsync(Func<HttpClient, Task> test)
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
            await test(client);
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

    private static async Task<HttpStatusCode> SendAsync(HttpClient client, string path, string json, string? token,
        string? origin)
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