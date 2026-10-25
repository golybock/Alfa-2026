using System.Security.Cryptography;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Access;

public sealed record LoginResult(string Token, DateTimeOffset ExpiresAt, MeResponse Me);

public sealed class AuthService(AlfaNewsDbContext db, TimeProvider timeProvider)
{
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"));

    public static string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public async Task<LoginResult> LoginAsync(LoginRequest request, TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrEmpty(request.Password)
                                                     || request.Login.Length > 128 || request.Password.Length > 256)
            throw new AccessException(400, "invalid_input", "Укажите логин и пароль.");

        var login = request.Login.Trim();
        var user = await db.AppUserRows.SingleOrDefaultAsync(row => row.Login == login, cancellationToken);
        var passwordValid = PasswordMatches(request.Password, user?.PasswordHash ?? DummyHash);
        if (user is null || !user.Active || !passwordValid)
        {
            // Учётки засевает Worker после импорта CSV; до этого вход отвечает «данные загружаются», а не «неверный пароль».
            if (!await db.CompanyRows.AsNoTracking().AnyAsync(row => row.ClType == "CLIENT", cancellationToken))
                throw new AccessException(503, "data_not_ready",
                    "Данные компаний ещё загружаются. Попробуйте войти через минуту.");
            throw new AccessException(401, "unauthorized", "Неверный логин или пароль.");
        }

        var now = timeProvider.GetUtcNow();
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var expiresAt = now.Add(lifetime);
        db.AuthSessionRows.Add(new AuthSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = SessionService.HashToken(token),
            CreatedAt = now,
            ExpiresAt = expiresAt
        });
        await db.SaveChangesAsync(cancellationToken);
        return new LoginResult(token, expiresAt, await ReadMeAsync(user, cancellationToken));
    }

    private static bool PasswordMatches(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

    public async Task<MeResponse> MeAsync(UserContext context, CancellationToken cancellationToken)
    {
        var user = await db.AppUserRows.AsNoTracking().SingleOrDefaultAsync(
            row => row.Id == context.UserId && row.Active,
            cancellationToken) ?? throw new AccessException(401, "unauthorized", "Требуется вход в систему.");
        return await ReadMeAsync(user, cancellationToken);
    }

    public async Task LogoutAsync(UserContext context, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await db.AuthSessionRows.Where(row => row.Id == context.SessionId && row.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.RevokedAt, now), cancellationToken);
    }

    private async Task<MeResponse> ReadMeAsync(AppUserEntity user, CancellationToken cancellationToken)
    {
        var rows = await (from access in db.UserCompanyAccessRows.AsNoTracking()
            join company in db.CompanyRows.AsNoTracking() on access.CompanyId equals company.Id
            where access.UserId == user.Id && company.ClType == "CLIENT"
            select new { company.Id, company.MainOkvedName, access.Role }).ToListAsync(cancellationToken);
        var companies = rows.GroupBy(row => new { row.Id, row.MainOkvedName })
            .OrderBy(group => group.Key.Id, StringComparer.Ordinal)
            .Select(group => new CompanyAccessDto(
                group.Key.Id,
                group.Key.MainOkvedName is null ? group.Key.Id : $"{group.Key.Id} · {group.Key.MainOkvedName}",
                group.Select(row => row.Role).Distinct().Order(StringComparer.Ordinal).Select(ParseRole).ToList()))
            .ToList();
        return new MeResponse(user.Id.ToString(), user.DisplayName, user.IsOperator, companies,
            AccessService.CanAddMaterials(user));
    }

    private static Role ParseRole(string role) => role switch
    {
        "ceo" => Role.Ceo,
        "cfo" => Role.Cfo,
        _ => throw new InvalidOperationException("В доступе сохранена неизвестная роль.")
    };
}