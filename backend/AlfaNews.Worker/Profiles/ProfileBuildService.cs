using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Profiles;

namespace AlfaNews.Worker.Profiles;

public sealed class ProfileBuildService(
    IConfiguration configuration,
    ProfileBuilder builder,
    ILogger<ProfileBuildService> logger)
{
    public async Task BuildOnStartupAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("Data:BuildProfilesOnStartup"))
            return;
        var version = configuration["Data:DatasetVersion"]
                      ?? throw new InvalidOperationException("Задайте Data__DatasetVersion для сборки профилей.");
        await RunAsync(new RebuildProfilesJob(version), true, cancellationToken);
    }

    public Task BuildAsync(RebuildProfilesJob job, CancellationToken cancellationToken, Guid? backgroundJobId = null) =>
        RunAsync(job, false, cancellationToken, backgroundJobId);

    private async Task RunAsync(RebuildProfilesJob job, bool onlyIfCurrent, CancellationToken cancellationToken,
        Guid? backgroundJobId = null)
    {
        var path = configuration["Data:ProfilesReportPath"] ?? "data/reports/profile_candidates.json";
        var result = await builder.BuildAsync(job, path, onlyIfCurrent, cancellationToken, backgroundJobId);
        if (result.Skipped)
            logger.LogInformation(
                "Профили {DataVersion} актуальны: компаний {Companies}, групп {Groups}. Пересборка пропущена.",
                result.Report.DataVersion, result.Report.CompanyCount, result.Report.GroupCount);
        else
            logger.LogInformation(
                "Профили {DataVersion} сохранены: компаний {Companies}, групп {Groups}. Отчёт: {ReportPath}",
                result.Report.DataVersion, result.Report.CompanyCount, result.Report.GroupCount, path);
    }
}