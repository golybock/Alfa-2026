using AlfaNews.Api.Access;
using AlfaNews.Core.Access;
using AlfaNews.Core.Processing;

namespace AlfaNews.Api.Jobs;

public static class JobEndpoints
{
    public static void MapJobEndpoints(this WebApplication app)
    {
        app.MapGet("/api/jobs/{id:guid}", async (Guid id, HttpContext http, AccessService access,
            CancellationToken cancellationToken) =>
        {
            var job = await access.RequireJobAsync(SessionAuthenticationHandler.CurrentUser(http.User), id,
                cancellationToken);
            return Results.Ok(JobQueue.ToResponse(job, http.TraceIdentifier));
        }).RequireAuthorization();
    }
}