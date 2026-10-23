using System.Text.Json;
using AlfaNews.Core.Documents;

namespace AlfaNews.Tests;

public sealed class PdfExtractionChecksTests
{
    [Theory]
    [InlineData("", "2026", "%", "1", "null", "null", "pdf:missing_point_context")]
    [InlineData("Ряд", "", "%", "1", "null", "null", "pdf:missing_point_context")]
    [InlineData("Ряд", "2026", "", "1", "null", "null", "pdf:missing_point_context")]
    [InlineData("Ряд", "2026", "%", "1", "0", "2", "pdf:invalid_point_value")]
    [InlineData("Ряд", "2026", "%", "null", "2", "1", "pdf:invalid_point_value")]
    [InlineData("Ряд", "2026", "%", "null", "null", "null", "pdf:invalid_point_value")]
    public void RejectsNumbersWithoutContextAndInvalidIntervals(string series, string period, string unit, string value, string low, string high, string error)
    {
        var json = JsonSerializer.Deserialize<JsonElement>($$"""
            {"blocks":[{"local_id":"b001","kind":"fact","text":"График","tables":[],"tags":{"factors":[],"okved2":[],"countries":[],"regions":[],"commodities":[]},
            "charts":[{"local_id":"c001","title":"График","description":"Описание","x_axis":null,"y_axis":null,"unit":null,"period":null,"legend":[],"region":[0,0,1,1],
            "points":[{"series":"{{series}}","period":"{{period}}","unit":"{{unit}}","value":{{value}},"low":{{low}},"high":{{high}},"approximate":false,"reading_method":"label"}]}]}]}
            """);
        Assert.Contains(error, PdfExtractionChecks.Validate(json));
    }

    [Fact]
    public void AcceptsDescriptionWithoutUnreadableChartNumbers()
    {
        var json = JsonSerializer.Deserialize<JsonElement>("""
            {"blocks":[{"local_id":"b001","kind":"fact","text":"График","tables":[],"tags":{"factors":[],"okved2":[],"countries":[],"regions":[],"commodities":[]},
            "charts":[{"local_id":"c001","title":"График","description":"Подписи не читаются, численные значения не извлечены.","x_axis":null,"y_axis":null,"unit":null,"period":null,"legend":[],"region":[0,0,1,1],"points":[]}]}]}
            """);
        Assert.Empty(PdfExtractionChecks.Validate(json));
    }
}