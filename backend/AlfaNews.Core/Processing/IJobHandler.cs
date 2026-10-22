using AlfaNews.Core.Data.Entities;

namespace AlfaNews.Core.Processing;

public interface IJobHandler
{
    string Kind { get; }
    Task<JobResult> HandleAsync(BackgroundJobEntity job, CancellationToken cancellationToken);
}