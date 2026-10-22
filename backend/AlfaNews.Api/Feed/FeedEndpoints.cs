using AlfaNews.Api.Access;
using AlfaNews.Core.Feed;

namespace AlfaNews.Api.Feed;

public static class FeedEndpoints
{
    public static void MapFeedEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/companies/{companyId}").RequireAuthorization();

        group.MapGet("/feed", async (string companyId, string? role, HttpContext http, FeedService feed,
                CancellationToken cancellationToken) =>
            Results.Ok(await feed.GetAsync(SessionAuthenticationHandler.CurrentUser(http.User), companyId,
                RoleQuery.Parse(role), cancellationToken)));

        group.MapPost("/feed/refresh", async (string companyId, HttpContext http, FeedService feed,
                CancellationToken cancellationToken) =>
            Results.Json(await feed.RefreshAsync(SessionAuthenticationHandler.CurrentUser(http.User), companyId,
                cancellationToken), statusCode: StatusCodes.Status202Accepted));
    }
}