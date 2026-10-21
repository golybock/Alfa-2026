using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlfaNews.Contracts;

public static class ContractJson
{
    public static void Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.PropertyNameCaseInsensitive = false;
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        options.RespectNullableAnnotations = true;
        options.RespectRequiredConstructorParameters = true;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.NumberHandling = JsonNumberHandling.Strict;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
    }
}