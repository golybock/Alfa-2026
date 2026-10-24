using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using AlfaNews.Contracts;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;

namespace AlfaNews.Tests;

public sealed class ContractJsonTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static TheoryData<string, Type> Examples => new()
    {
        { "activity.json", typeof(UserActivityRequest) },
        { "me.json", typeof(MeResponse) },
        { "feed-ready.json", typeof(FeedResponse) },
        { "feed-pending.json", typeof(FeedResponse) },
        { "feed-failed.json", typeof(FeedResponse) },
        { "insight-steps.json", typeof(InsightStepsResponse) },
        { "feed-empty.json", typeof(FeedResponse) },
        { "feed-company-b.json", typeof(FeedResponse) },
        { "insight.json", typeof(InsightDto) },
        { "insight-ceo.json", typeof(InsightDto) },
        { "insight-no-bank.json", typeof(InsightDto) },
        { "insight-company-b.json", typeof(InsightDto) },
        { "insight-company-b-ceo.json", typeof(InsightDto) },
        { "job-queued.json", typeof(JobResponse) },
        { "job-done.json", typeof(JobResponse) },
        { "job-failed.json", typeof(JobResponse) },
        { "chat-answer.json", typeof(ChatAnswerDto) },
        { "chat-refusal.json", typeof(ChatAnswerDto) },
        { "documents-list.json", typeof(DocumentListResponse) },
        { "document.json", typeof(DocumentResponse) },
        { "manual-news-list.json", typeof(ManualNewsListResponse) },
        { "news-sources.json", typeof(NewsSourcesResponse) },
        { "page-review.json", typeof(PageReviewRequest) },
        { "news-extraction.json", typeof(NewsExtraction) }
    };

    [Theory]
    [MemberData(nameof(Examples))]
    public void ExamplesRoundTripWithoutChangingJson(string fileName, Type type)
    {
        var json = ReadExample(fileName);
        var value = JsonSerializer.Deserialize(json, type, Options);

        Assert.NotNull(value);
        var actual = JsonSerializer.SerializeToNode(value, type, Options);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), actual), fileName);
    }

    [Fact]
    public void EveryExampleHasAType()
    {
        var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Examples"), "*.json")
            .Select(Path.GetFileName).Order().ToArray();
        var covered = Examples.Select(example => (string)example[0]).Order().ToArray();

        Assert.Equal(files, covered);
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void RequiredConstructorFieldsCannotBeOmitted(string fileName, Type type)
    {
        var constructor = type.GetConstructors().Single(candidate => candidate.IsPublic);
        foreach (var parameter in constructor.GetParameters().Where(parameter => !parameter.HasDefaultValue))
        {
            var json = JsonNode.Parse(ReadExample(fileName))!.AsObject();
            var name = JsonNamingPolicy.SnakeCaseLower.ConvertName(parameter.Name!);
            Assert.True(json.Remove(name));

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json.ToJsonString(), type, Options));
        }
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void NonNullableConstructorFieldsCannotBeNull(string fileName, Type type)
    {
        var nullability = new NullabilityInfoContext();
        var constructor = type.GetConstructors().Single(candidate => candidate.IsPublic);
        foreach (var parameter in constructor.GetParameters()
                     .Where(parameter => nullability.Create(parameter).ReadState == NullabilityState.NotNull))
        {
            var json = JsonNode.Parse(ReadExample(fileName))!.AsObject();
            var name = JsonNamingPolicy.SnakeCaseLower.ConvertName(parameter.Name!);
            json[name] = null;

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json.ToJsonString(), type, Options));
        }
    }

    [Theory]
    [InlineData("{\"role\":0}")]
    [InlineData("{\"role\":\"0\"}")]
    [InlineData("{\"role\":\"unknown\"}")]
    [InlineData("{\"Role\":\"ceo\"}")]
    [InlineData("{\"role\":\"ceo\",\"company_id\":\"other\"}")]
    public void InvalidRequestsAreRejected(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<InsightRequest>(json, Options));
    }

    [Fact]
    public void UnknownNestedFieldsAreRejected()
    {
        var json = JsonNode.Parse(ReadExample("insight.json"))!;
        json["claims"]![0]!["kind"] = "fact";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<InsightDto>(json.ToJsonString(), Options));
    }

    [Fact]
    public void StringNumbersAreRejected()
    {
        var json = JsonNode.Parse(ReadExample("feed-ready.json"))!;
        json["items"]![0]!["relevance"]!["score"] = "0.24";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<FeedResponse>(json.ToJsonString(), Options));
    }

    [Fact]
    public void NeutralConsequenceUsesWireName()
    {
        const string json = """{"type":"neutral","claim_ids":["C1"]}""";
        var consequence = JsonSerializer.Deserialize<ConsequenceDto>(json, Options);

        Assert.Equal(ConsequenceType.Neutral, consequence!.Type);
        Assert.Equal(json, JsonSerializer.Serialize(consequence, Options));
    }

    [Fact]
    public void NullForecastIsOmittedAndOtherNullableFieldsArePreserved()
    {
        var insight = JsonSerializer.Deserialize<InsightDto>(ReadExample("insight.json"), Options)!;
        var json = JsonSerializer.SerializeToNode(insight, Options)!.AsObject();

        Assert.False(json.ContainsKey("forecast"));
        var evidence = json["evidence"]!["E1"]!.AsObject();
        Assert.True(evidence.ContainsKey("document_id"));
        Assert.Null(evidence["document_id"]);
    }

    [Theory]
    [InlineData(Role.Ceo, "ceo")]
    [InlineData(Role.Cfo, "cfo")]
    public void RolesUseWireNames(Role role, string wireName)
    {
        var request = JsonSerializer.Deserialize<InsightRequest>($$"""{"role":"{{wireName}}"}""", Options);

        Assert.Equal(role, request!.Role);
        Assert.Equal($$"""{"role":"{{wireName}}"}""", JsonSerializer.Serialize(request, Options));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ContractJson.Configure(options);
        return options;
    }

    private static string ReadExample(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Examples", fileName));
}