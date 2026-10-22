using System.Text.Json;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Processing;

namespace AlfaNews.Core.Documents;

public static class PdfExtractionChecks
{
    public const string Version = "pdf-page-v1";

    public static IReadOnlyList<string> Validate(JsonElement json)
    {
        var extraction = json.Deserialize<PdfPageExtraction>(JobKinds.JsonOptions)!;
        var errors = new List<string>();
        foreach (var block in extraction.Blocks)
        {
            if (string.IsNullOrWhiteSpace(block.LocalId)
                || string.IsNullOrWhiteSpace(block.Text) && block.Tables.Count == 0 && block.Charts.Count == 0)
                errors.Add("pdf:empty_block");
            foreach (var chart in block.Charts)
            {
                if (chart.Region.Count != 4 || chart.Region[2] <= 0 || chart.Region[3] <= 0)
                    errors.Add("pdf:empty_chart_region");
                foreach (var point in chart.Points)
                {
                    if (string.IsNullOrWhiteSpace(point.Series) || string.IsNullOrWhiteSpace(point.Period) || string.IsNullOrWhiteSpace(point.Unit))
                        errors.Add("pdf:missing_point_context");
                    if (point.Value is not null ? point.Low is not null || point.High is not null
                        : point.Low is null || point.High is null || point.Low > point.High)
                        errors.Add("pdf:invalid_point_value");
                }
            }
        }
        return errors;
    }
}