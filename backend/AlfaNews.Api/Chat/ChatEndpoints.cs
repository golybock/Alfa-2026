using AlfaNews.Api.Access;
using AlfaNews.Contracts.Api;
using AlfaNews.Core.Chat;

namespace AlfaNews.Api.Chat;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/insights/{id:guid}/chat").RequireAuthorization();

        group.MapPost("", async (Guid id, ChatRequest request, HttpContext http, ChatService chat,
            CancellationToken cancellationToken) =>
        {
            var result = await chat.AskAsync(SessionAuthenticationHandler.CurrentUser(http.User), id, request,
                cancellationToken);
            return result.Answer is not null
                ? Results.Ok(result.Answer)
                : Results.Json(result.Accepted, statusCode: StatusCodes.Status202Accepted);
        });

        group.MapGet("", async (Guid id, HttpContext http, ChatService chat, CancellationToken cancellationToken) =>
            Results.Ok(await chat.HistoryAsync(SessionAuthenticationHandler.CurrentUser(http.User), id,
                cancellationToken)));
    }
}