using AlfaNews.Core.Data;
using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Access;

public sealed record UserSeedOptions(
    string UserLogin,
    string UserPassword,
    string OperatorLogin,
    string OperatorPassword,
    IReadOnlyList<string> CompanyIds);

public sealed class UserSeedService(AlfaNewsDbContext db)
{
    private static readonly string[] Roles = ["ceo", "cfo"];

    public async Task SeedAsync(UserSeedOptions options, CancellationToken cancellationToken)
    {
        var existingCompanies = await db.CompanyRows.Where(company => company.ClType == "CLIENT"
            && options.CompanyIds.Contains(company.Id)).Select(company => company.Id).ToListAsync(cancellationToken);
        var missing = options.CompanyIds.Except(existingCompanies).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Компании CLIENT не найдены: {string.Join(", ", missing)}.");

        var user = await UpsertUserAsync(options.UserLogin, options.UserPassword, "Демо-пользователь", false, cancellationToken);
        var operatorUser = await UpsertUserAsync(options.OperatorLogin, options.OperatorPassword, "Оператор", true, cancellationToken);

        await db.UserCompanyAccessRows.Where(access => access.UserId == user.Id || access.UserId == operatorUser.Id)
            .ExecuteDeleteAsync(cancellationToken);
        db.UserCompanyAccessRows.AddRange(from companyId in options.CompanyIds
                                          from role in Roles
                                          select new UserCompanyAccessEntity { UserId = user.Id, CompanyId = companyId, Role = role });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AppUserEntity> UpsertUserAsync(string login, string password, string displayName, bool isOperator,
        CancellationToken cancellationToken)
    {
        var user = await db.AppUserRows.SingleOrDefaultAsync(row => row.Login == login, cancellationToken);
        if (user is null)
        {
            user = new AppUserEntity { Id = Guid.NewGuid(), Login = login };
            db.AppUserRows.Add(user);
        }
        user.PasswordHash = AuthService.HashPassword(password);
        user.DisplayName = displayName;
        user.IsOperator = isOperator;
        user.Active = true;
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }
}