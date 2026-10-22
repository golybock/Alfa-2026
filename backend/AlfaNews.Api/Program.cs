using AlfaNews.Contracts;
using AlfaNews.Api.Access;
using AlfaNews.Api.Activity;
using AlfaNews.Core.Access;
using AlfaNews.Core.Activity;
using AlfaNews.Core.Data;
using AlfaNews.Core.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

LocalEnvironment.Load();
var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options => ContractJson.Configure(options.SerializerOptions));
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

builder.Services.AddAlfaNewsData(builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Задайте ConnectionStrings__DefaultConnection."));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<AccessService>();
builder.Services.AddScoped<UserActivityService>();
builder.Services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization(options => options.AddPolicy(SessionAuthenticationHandler.OperatorPolicy,
    policy => policy.RequireAuthenticatedUser().RequireClaim("operator", "true")));

builder.Services.AddOpenApi();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<AlfaNewsDbContext>().Database.MigrateAsync(app.Lifetime.ApplicationStopping);
if (args.Contains("--migrate"))
    return;

app.UseMiddleware<ApiAccessMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapActivityEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();

public partial class Program;