using AlfaNews.Contracts.Api;
using AlfaNews.Core.Access;

namespace AlfaNews.Api.Access;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api");

        group.MapPost("/auth/login", async (LoginRequest request, HttpContext http, AuthService auth,
            IConfiguration configuration, CancellationToken cancellationToken) =>
        {
            var hours = configuration.GetValue("Auth:SessionHours", 12);
            var result = await auth.LoginAsync(request, TimeSpan.FromHours(hours), cancellationToken);
            http.Response.Cookies.Append(SessionAuthenticationHandler.CookieName, result.Token, CookieOptions(http, result.ExpiresAt));
            return Results.Ok(result.Me);
        });

        group.MapPost("/auth/logout", async (HttpContext http, AuthService auth, CancellationToken cancellationToken) =>
        {
            await auth.LogoutAsync(SessionAuthenticationHandler.CurrentUser(http.User), cancellationToken);
            http.Response.Cookies.Delete(SessionAuthenticationHandler.CookieName, CookieOptions(http, null));
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapGet("/me", async (HttpContext http, AuthService auth, CancellationToken cancellationToken) =>
            Results.Ok(await auth.MeAsync(SessionAuthenticationHandler.CurrentUser(http.User), cancellationToken)))
            .RequireAuthorization();
    }

    private static CookieOptions CookieOptions(HttpContext http, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = http.Request.IsHttps || http.Request.Headers["X-Forwarded-Proto"] == "https",
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = expires
    };
}
