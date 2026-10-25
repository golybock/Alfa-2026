using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Core.Access;
using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Tests;

public sealed class AuthServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task LoginReturnsOnlyAllowedCompaniesAndLogoutRevokesSession()
    {
        var login = "auth_" + Guid.NewGuid().ToString("N");
        await using var db = fixture.CreateDb();
        var user = new AppUserEntity
        {
            Id = Guid.NewGuid(),
            Login = login,
            PasswordHash = AuthService.HashPassword("correct-password"),
            DisplayName = "Тест"
        };
        db.AppUserRows.Add(user);
        db.UserCompanyAccessRows.Add(new UserCompanyAccessEntity
            { UserId = user.Id, CompanyId = "test_company_a", Role = "cfo" });
        await db.SaveChangesAsync();
        var auth = new AuthService(db, TimeProvider.System);

        var wrong = await Assert.ThrowsAsync<AccessException>(() =>
            auth.LoginAsync(new LoginRequest(login, "wrong"), TimeSpan.FromHours(1), CancellationToken.None));
        Assert.Equal(401, wrong.StatusCode);

        var result = await auth.LoginAsync(new LoginRequest(login, "correct-password"), TimeSpan.FromHours(1),
            CancellationToken.None);
        var company = Assert.Single(result.Me.Companies);
        Assert.Equal("test_company_a", company.CompanyId);
        Assert.Equal([Role.Cfo], company.Roles);

        var session = await db.AuthSessionRows.AsNoTracking().SingleAsync(row => row.UserId == user.Id);
        Assert.NotEqual(result.Token, session.TokenHash);
        Assert.Equal(SessionService.HashToken(result.Token), session.TokenHash);

        var sessions = new SessionService(db, TimeProvider.System);
        var context = await sessions.ResolveAsync(result.Token, CancellationToken.None);
        Assert.NotNull(context);
        await auth.LogoutAsync(context, CancellationToken.None);
        Assert.Null(await sessions.ResolveAsync(result.Token, CancellationToken.None));
    }

    [PostgresFact]
    public async Task SeedCreatesOperatorsDemoAndOneUserPerCompanyWithStaticPassword()
    {
        await using var db = fixture.CreateDb();
        var seed = new UserSeedService(db);
        var auth = new AuthService(db, TimeProvider.System);
        var lifetime = TimeSpan.FromHours(1);

        var result = await seed.SeedAsync(["test_company_a", "test_company_b", "no_such_company"],
            CancellationToken.None);
        Assert.Equal(["admin", "operator", "demo", "test_company_a", "test_company_b"], result.Logins);
        Assert.Equal(["no_such_company"], result.MissingCompanies);
        await seed.SeedAsync(["test_company_a", "test_company_b"], CancellationToken.None);

        foreach (var login in new[] { "admin", "operator" })
        {
            var me = (await auth.LoginAsync(new LoginRequest(login, UserSeedService.Password), lifetime,
                CancellationToken.None)).Me;
            Assert.True(me.IsOperator);
            Assert.Empty(me.Companies);
        }

        var demo = (await auth.LoginAsync(new LoginRequest("demo", UserSeedService.Password), lifetime,
            CancellationToken.None)).Me;
        Assert.False(demo.IsOperator);
        Assert.Equal(["test_company_a", "test_company_b"], demo.Companies.Select(company => company.CompanyId));
        Assert.All(demo.Companies, company => Assert.Equal([Role.Ceo, Role.Cfo], company.Roles));

        var single = (await auth.LoginAsync(new LoginRequest(" test_company_b ", UserSeedService.Password), lifetime,
            CancellationToken.None)).Me;
        var company = Assert.Single(single.Companies);
        Assert.Equal("test_company_b", company.CompanyId);
        Assert.Equal([Role.Ceo, Role.Cfo], company.Roles);

        var wrong = await Assert.ThrowsAsync<AccessException>(() =>
            auth.LoginAsync(new LoginRequest("demo", "wrong"), lifetime, CancellationToken.None));
        Assert.Equal(401, wrong.StatusCode);
    }
}