using System.Security.Claims;
using System.Text.Encodings.Web;
using AlfaNews.Contracts.Api;
using AlfaNews.Core.Access;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AlfaNews.Api.Access;

public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    SessionService sessions,
    AccessService access) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Session";
    public const string CookieName = "alfa_session";
    public const string OperatorPolicy = "Operator";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(CookieName, out var token))
            return AuthenticateResult.NoResult();
        var context = await sessions.ResolveAsync(token, Context.RequestAborted);
        if (context is null)
            return AuthenticateResult.Fail("Недействительная сессия.");
        var user = await access.RequireUserAsync(context, Context.RequestAborted);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, context.UserId.ToString()),
            new("session_id", context.SessionId.ToString())
        };
        if (user.IsOperator)
            claims.Add(new Claim("operator", "true"));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        WriteErrorAsync(401, "unauthorized", "Требуется вход в систему.");

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        WriteErrorAsync(403, "forbidden", "Недостаточно прав.");

    private Task WriteErrorAsync(int status, string code, string message)
    {
        Response.StatusCode = status;
        return Response.WriteAsJsonAsync(new ApiError(code, message, Context.TraceIdentifier), Context.RequestAborted);
    }

    public static UserContext CurrentUser(ClaimsPrincipal principal)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(principal.FindFirstValue("session_id"), out var sessionId))
            throw new AccessException(401, "unauthorized", "Требуется вход в систему.");
        return new UserContext(userId, sessionId);
    }
}