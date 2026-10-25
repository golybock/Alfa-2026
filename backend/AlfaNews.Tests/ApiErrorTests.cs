using System.Text.Json;
using AlfaNews.Api.Access;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlfaNews.Tests;

public sealed class ApiErrorTests
{
    [Fact]
    public async Task UnhandledApiExceptionBecomesInternalErrorWithoutDetails()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-1" };
        context.Request.Method = "GET";
        context.Request.Path = "/api/feed";
        context.Response.Body = new MemoryStream();
        var middleware = new ApiAccessMiddleware(_ => throw new InvalidOperationException("секрет в тексте исключения"));

        await middleware.InvokeAsync(context, new ConfigurationBuilder().Build(), NullLogger<ApiAccessMiddleware>.Instance);

        Assert.Equal(500, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("internal_error", body.RootElement.GetProperty("code").GetString());
        Assert.Contains("trace-1", body.RootElement.GetRawText());
        Assert.DoesNotContain("секрет", body.RootElement.GetRawText());
    }

    [Fact]
    public async Task NonApiExceptionIsNotSwallowed()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/openapi/v1.json";
        var middleware = new ApiAccessMiddleware(_ => throw new InvalidOperationException());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.InvokeAsync(context, new ConfigurationBuilder().Build(), NullLogger<ApiAccessMiddleware>.Instance));
    }
}