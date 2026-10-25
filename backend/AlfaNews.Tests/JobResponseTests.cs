using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Processing;

namespace AlfaNews.Tests;

public sealed class JobResponseTests
{
    [Fact]
    public void ResponseDoesNotExposePayloadPathsOrExceptionText()
    {
        var job = new BackgroundJobEntity
        {
            Id = Guid.NewGuid(),
            Status = "failed",
            Stage = "failed",
            Payload = JsonSerializer.SerializeToElement(new { path = "private/file.csv", key = "private-key" }),
            LastError = "private exception password",
            ResultType = "csv_package",
            ResultId = "private-hash"
        };
        var response = JobQueue.ToResponse(job, "request-id");
        var json = JsonSerializer.Serialize(response, JobKinds.JsonOptions);
        Assert.DoesNotContain("private", json);
        Assert.Null(response.ResultType);
        Assert.Null(response.ResultId);
        Assert.Equal("job_failed", response.Error!.Code);
        Assert.Equal("request-id", response.Error.RequestId);
    }

    [Theory]
    [InlineData("not_implemented")]
    [InlineData("llm_key_required")]
    [InlineData("invalid_payload")]
    public void ResponsePreservesSafeErrorCode(string code)
    {
        var response =
            JobQueue.ToResponse(new BackgroundJobEntity { Status = "failed", Stage = "failed", LastError = code }, "r");
        Assert.Equal(code, response.Error!.Code);
    }

    [Fact]
    public void RegistersAllContractJobsAndPrioritizesFreshNewsAboveOcr()
    {
        var types = typeof(ParseNewsJob).Assembly.GetTypes().Where(type =>
            type.Namespace == typeof(ParseNewsJob).Namespace && type.Name.EndsWith("Job"));
        foreach (var type in types)
            Assert.Equal(type, JobKinds.PayloadType(JobKinds.Name(type)));
        Assert.True(JobKinds.Priority<ParseNewsJob>() > JobKinds.Priority<OcrPdfPageJob>());
        Assert.True(JobKinds.Priority<CollectNewsJob>() > JobKinds.Priority<GenerateInsightJob>());
    }
}