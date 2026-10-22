using AlfaNews.Contracts;
using AlfaNews.Api.Access;
using AlfaNews.Api.Activity;
using AlfaNews.Api.Feed;
using AlfaNews.Api.Insights;
using AlfaNews.Api.Jobs;
using AlfaNews.Core.Access;
using AlfaNews.Core.Activity;
using AlfaNews.Core.Data;
using AlfaNews.Core.Feed;
using AlfaNews.Core.Insights;
using AlfaNews.Core.Processing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options => ContractJson.Configure(options.SerializerOptions));
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

builder.Services.AddAlfaNewsData(builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Задайте ConnectionStrings__DefaultConnection."));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<AccessService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<JobQueue>();
builder.Services.AddScoped<FeedService>();
builder.Services.AddScoped<InsightService>();
builder.Services.AddScoped<UserSeedService>();
builder.Services.AddScoped<UserActivityService>();
builder.Services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization(options => options.AddPolicy(SessionAuthenticationHandler.OperatorPolicy,
    policy => policy.RequireAuthenticatedUser().RequireClaim("operator", "true")));

builder.Services.AddOpenApi();

var app = builder.Build();

if (args.Contains("--migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AlfaNewsDbContext>().Database.MigrateAsync(app.Lifetime.ApplicationStopping);
    return;
}

if (args.Contains("--seed-users"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var configuration = app.Configuration;
    var companyIds = (configuration["Seed:CompanyIds"] ?? throw new InvalidOperationException("Задайте Seed__CompanyIds."))
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    await scope.ServiceProvider.GetRequiredService<UserSeedService>().SeedAsync(new UserSeedOptions(
        configuration["Seed:UserLogin"] ?? "demo",
        configuration["Seed:UserPassword"] ?? throw new InvalidOperationException("Задайте Seed__UserPassword."),
        configuration["Seed:OperatorLogin"] ?? "operator",
        configuration["Seed:OperatorPassword"] ?? throw new InvalidOperationException("Задайте Seed__OperatorPassword."),
        companyIds), app.Lifetime.ApplicationStopping);
    return;
}

app.UseMiddleware<ApiAccessMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapActivityEndpoints();
app.MapFeedEndpoints();
app.MapInsightEndpoints();
app.MapJobEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();

public partial class Program;