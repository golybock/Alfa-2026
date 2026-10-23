using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Chat;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Processing;

namespace AlfaNews.Worker.Chat;

public sealed class AnswerChatJobHandler(ChatResponder responder) : IJobHandler
{
    public string Kind => JobKinds.Name<AnswerChatJob>();

    public Task<JobResult> HandleAsync(BackgroundJobEntity job, CancellationToken cancellationToken) =>
        responder.AnswerAsync(job, job.Payload.Deserialize<AnswerChatJob>(JobKinds.JsonOptions)!, cancellationToken);
}