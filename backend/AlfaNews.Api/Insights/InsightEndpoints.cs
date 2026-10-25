using AlfaNews.Api.Access;
using AlfaNews.Contracts.Api;
using AlfaNews.Core.Insights;

namespace AlfaNews.Api.Insights;

public static class InsightEndpoints
{
    public static void MapInsightEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/companies/{companyId}/events/{eventId}/insight").RequireAuthorization();

        group.MapGet("", async (string companyId, string eventId, string? role, HttpContext http,
            InsightService insights, CancellationToken cancellationToken) =>
        {
            var result = await insights.GetAsync(SessionAuthenticationHandler.CurrentUser(http.User), companyId,
                eventId,
                RoleQuery.Parse(role), cancellationToken);
            return result.Insight is not null
                ? Results.Ok(result.Insight)
                : Results.Json(result.Pending, statusCode: StatusCodes.Status202Accepted);
        });

        group.MapPost("", async (string companyId, string eventId, InsightRequest request, HttpContext http,
            InsightService insights, CancellationToken cancellationToken) =>
        {
            var result = await insights.EnsureAsync(SessionAuthenticationHandler.CurrentUser(http.User), companyId,
                eventId, request.Role, cancellationToken);
            return result.Insight is not null
                ? Results.Ok(result.Insight)
                : Results.Json(result.Accepted, statusCode: StatusCodes.Status202Accepted);
        });

        app.MapGet("/api/insights/{id:guid}/steps", async (Guid id, HttpContext http, InsightService insights,
                    CancellationToken cancellationToken) =>
                Results.Ok(await insights.StepsAsync(SessionAuthenticationHandler.CurrentUser(http.User), id,
                    cancellationToken)))
            .RequireAuthorization();
    }
}