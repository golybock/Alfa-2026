using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Access;

public sealed record UserSeedResult(IReadOnlyList<string> Logins, IReadOnlyList<string> MissingCompanies);

/// <summary>Демо-учётки с общим статическим паролем: операторы, demo со всеми компаниями ленты и по учётке на компанию.</summary>
public sealed class UserSeedService(AlfaNewsDbContext db)
{
    public const string Password = "password";
    public const string AllCompaniesLogin = "demo";

    public static readonly IReadOnlyList<(string Login, string DisplayName)> Operators =
        [("admin", "Администратор"), ("operator", "Оператор")];

    private static readonly string[] Roles = ["ceo", "cfo"];

    public async Task<UserSeedResult> SeedAsync(IReadOnlyList<string> companyIds, CancellationToken cancellationToken)
    {
        var existing = await db.CompanyRows.AsNoTracking().Where(company => company.ClType == "CLIENT"
                                                                            && companyIds.Contains(company.Id))
            .Select(company => company.Id).ToListAsync(cancellationToken);
        var companies = companyIds.Distinct(StringComparer.Ordinal).Where(existing.Contains).ToList();
        var logins = new List<string>();

        foreach (var (login, displayName) in Operators)
            logins.Add(await UpsertUserAsync(login, displayName, true, [], cancellationToken));
        if (companies.Count > 0)
            logins.Add(await UpsertUserAsync(AllCompaniesLogin, "Демо-пользователь", false, companies,
                cancellationToken));
        foreach (var companyId in companies)
            logins.Add(await UpsertUserAsync(companyId, $"Компания {companyId}", false, [companyId],
                cancellationToken));

        return new UserSeedResult(logins, companyIds.Except(existing, StringComparer.Ordinal).ToList());
    }

    private async Task<string> UpsertUserAsync(string login, string displayName, bool isOperator,
        IReadOnlyList<string> companyIds,
        CancellationToken cancellationToken)
    {
        var user = await db.AppUserRows.SingleOrDefaultAsync(row => row.Login == login, cancellationToken);
        if (user is null)
        {
            user = new AppUserEntity { Id = Guid.NewGuid(), Login = login };
            db.AppUserRows.Add(user);
        }

        user.PasswordHash = AuthService.HashPassword(Password);
        user.DisplayName = displayName;
        user.IsOperator = isOperator;
        user.Active = true;
        var wanted = (from companyId in companyIds from role in Roles select (companyId, role)).ToHashSet();
        var current = await db.UserCompanyAccessRows.Where(access => access.UserId == user.Id)
            .ToListAsync(cancellationToken);
        db.UserCompanyAccessRows.RemoveRange(current.Where(access =>
            !wanted.Contains((access.CompanyId, access.Role))));
        wanted.ExceptWith(current.Select(access => (access.CompanyId, access.Role)));
        db.UserCompanyAccessRows.AddRange(wanted.Select(access =>
            new UserCompanyAccessEntity { UserId = user.Id, CompanyId = access.companyId, Role = access.role }));
        await db.SaveChangesAsync(cancellationToken);
        return login;
    }
}