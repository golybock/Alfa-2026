using AlfaNews.Api.Access;
using AlfaNews.Api.Activity;
using AlfaNews.Api.Chat;
using AlfaNews.Api.Documents;
using AlfaNews.Api.Feed;
using AlfaNews.Api.Insights;
using AlfaNews.Api.Jobs;
using AlfaNews.Contracts;
using AlfaNews.Core.Access;
using AlfaNews.Core.Activity;
using AlfaNews.Core.Chat;
using AlfaNews.Core.Configuration;
using AlfaNews.Core.Data;
using AlfaNews.Core.Documents;
using AlfaNews.Core.Feed;
using AlfaNews.Core.Insights;
using AlfaNews.Core.Processing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

LocalEnvironment.Load();
var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options => ContractJson.Configure(options.SerializerOptions));
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

builder.Services.AddAlfaNewsData(builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Задайте ConnectionStrings__DefaultConnection."));
builder.Services.AddSingleton(TimeProvider.System);
var pdfOptions = new PdfOptions();
builder.Configuration.GetSection("Storage").Bind(pdfOptions);
pdfOptions.InputPath = builder.Configuration["Data:InputPath"] ?? pdfOptions.InputPath;
builder.Services.AddSingleton(pdfOptions);
builder.Services.AddScoped<PdfTools>();
builder.Services.AddScoped<PdfRegistration>();
builder.Services.AddScoped<ResearchReview>();
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 52L * 1024 * 1024);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 52L * 1024 * 1024);
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<AccessService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<JobQueue>();
var feedSettings = new FeedSettings();
builder.Configuration.GetSection("Feed").Bind(feedSettings);
feedSettings.Validate();
builder.Services.AddSingleton(feedSettings);
builder.Services.AddScoped<FeedService>();
builder.Services.AddScoped<InsightService>();
builder.Services.AddScoped<UserActivityService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<FeedbackService>();
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
app.MapAuthEndpoints();
app.MapActivityEndpoints();
app.MapChatEndpoints();
app.MapDocumentEndpoints();
app.MapFeedEndpoints();
app.MapInsightEndpoints();
app.MapJobEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapOpenApi("/api/openapi/{documentName}.json");
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "api/swagger";
        options.SwaggerEndpoint("../openapi/v1.json", "Новостная аналитика v1");
        options.DocumentTitle = "Новостная аналитика — Swagger";
        options.EnableValidator(null);
    });
}

app.UseHttpsRedirection();

app.Run();

namespace AlfaNews.Api
{
    public partial class Program;
}