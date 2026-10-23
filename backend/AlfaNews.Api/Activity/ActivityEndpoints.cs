using AlfaNews.Api.Access;
using AlfaNews.Contracts.Api;
using AlfaNews.Core.Access;
using AlfaNews.Core.Activity;

namespace AlfaNews.Api.Activity;

public static class ActivityEndpoints
{
    public static void MapActivityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/insights").RequireAuthorization();
        group.MapPost("/{id:guid}/activity", async (Guid id, UserActivityRequest request,
            HttpContext http, UserActivityService activities, CancellationToken cancellationToken) =>
        {
            await activities.RecordAsync(SessionAuthenticationHandler.CurrentUser(http.User), id, request, cancellationToken);
            return Results.NoContent();
        });
        group.MapPost("/{id:guid}/feedback", async (Guid id, FeedbackRequest request,
            HttpContext http, FeedbackService feedback, CancellationToken cancellationToken) =>
        {
            await feedback.SaveAsync(SessionAuthenticationHandler.CurrentUser(http.User), id, request, cancellationToken);
            return Results.NoContent();
        });
        group.MapGet("/{id:guid}/evidence/{evidenceId}", async (Guid id, string evidenceId, HttpContext http,
            AccessService access, CancellationToken cancellationToken) =>
            Results.Ok(await access.RequireEvidenceAsync(SessionAuthenticationHandler.CurrentUser(http.User), id, evidenceId,
                cancellationToken)));
    }
}