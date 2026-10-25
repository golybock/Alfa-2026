using AlfaNews.Contracts.Api;
using AlfaNews.Core.Access;

namespace AlfaNews.Api.Access;

public sealed class ApiAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IConfiguration configuration,
        ILogger<ApiAccessMiddleware> logger)
    {
        try
        {
            if (context.Request.Path.StartsWithSegments("/api") && IsMutation(context.Request.Method))
            {
                var origins = configuration.GetSection("Auth:AllowedOrigins").Get<string[]>()
                              ?? [$"{context.Request.Scheme}://{context.Request.Host}"];
                var supplied = context.Request.Headers.Origin;
                if (supplied.Count != 1 || !Uri.TryCreate(supplied[0], UriKind.Absolute, out var origin)
                                        || origin.Scheme is not ("http" or "https") || origin.AbsolutePath != "/"
                                        || origin.Query.Length != 0 || origin.Fragment.Length != 0 ||
                                        origin.UserInfo.Length != 0
                                        || !origins.Any(allowed => string.Equals(allowed.TrimEnd('/'),
                                            origin.GetLeftPart(UriPartial.Authority),
                                            StringComparison.OrdinalIgnoreCase)))
                    throw new AccessException(403, "invalid_origin", "Источник запроса не разрешён.");
            }

            await next(context);
        }
        catch (AccessException exception)
        {
            context.Response.StatusCode = exception.StatusCode;
            await context.Response.WriteAsJsonAsync(
                new ApiError(exception.Code, exception.Message, context.TraceIdentifier), context.RequestAborted);
        }
        catch (BadHttpRequestException exception)
        {
            context.Response.StatusCode = exception.StatusCode;
            await context.Response.WriteAsJsonAsync(
                new ApiError("invalid_input", "Некорректный запрос.", context.TraceIdentifier), context.RequestAborted);
        }
        catch (Exception exception) when (context.Request.Path.StartsWithSegments("/api") &&
                                          !context.Response.HasStarted
                                          && !context.RequestAborted.IsCancellationRequested)
        {
            // Контракт: все ошибки API — ApiError без подробностей; подробности только в логе по request_id.
            logger.LogError(exception, "Необработанная ошибка API {Method} {Path}, request_id {RequestId}.",
                context.Request.Method, context.Request.Path.Value, context.TraceIdentifier);
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(
                new ApiError("internal_error", "Внутренняя ошибка сервера. Повторите попытку позже.",
                    context.TraceIdentifier),
                context.RequestAborted);
        }
    }

    private static bool IsMutation(string method) =>
        !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method);
}