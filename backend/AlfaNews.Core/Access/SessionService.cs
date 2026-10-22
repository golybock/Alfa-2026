using System.Security.Cryptography;
using System.Text;
using AlfaNews.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Access;

public sealed class SessionService(AlfaNewsDbContext db, TimeProvider timeProvider)
{
    public static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task<UserContext?> ResolveAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 256)
            return null;

        var hash = HashToken(token);
        var now = timeProvider.GetUtcNow();
        return await (from session in db.AuthSessionRows.AsNoTracking()
            join user in db.AppUserRows.AsNoTracking() on session.UserId equals user.Id
            where session.TokenHash == hash && session.RevokedAt == null && session.ExpiresAt > now && user.Active
            select new UserContext(user.Id, session.Id)).SingleOrDefaultAsync(cancellationToken);
    }
}