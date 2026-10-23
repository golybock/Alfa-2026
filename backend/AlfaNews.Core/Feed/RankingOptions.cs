namespace AlfaNews.Core.Feed;

public sealed class RankingOptions
{
    public decimal OkvedWeight { get; set; } = 0.25m;
    public decimal FactorWeight { get; set; } = 0.25m;
    public decimal ProductWeight { get; set; } = 0.20m;
    public decimal CountryWeight { get; set; } = 0.10m;
    public decimal CpWeight { get; set; } = 0.15m;
    public decimal GroupWeight { get; set; } = 0.05m;
    public decimal MinBase { get; set; } = 0.20m;
    public decimal MinScore { get; set; } = 0.15m;
    public int MaxItems { get; set; } = 5;
    public bool CpEnabled { get; set; } = true;
    public bool PrecomputeInsights { get; set; } = true;
    public string ConfigVersion { get; set; } = "ranking-v1";
    public IReadOnlyList<string> CompanyIds { get; set; } = [];

    public void Validate()
    {
        var weights = new[] { OkvedWeight, FactorWeight, ProductWeight, CountryWeight, CpWeight, GroupWeight };
        if (weights.Any(weight => weight is < 0 or > 1) || weights.Sum() != 1m || MinBase is < 0 or > 1
            || MinScore is < 0 or > 1 || MaxItems is < 1 or > 5 || string.IsNullOrWhiteSpace(ConfigVersion))
            throw new InvalidOperationException("Некорректные настройки ранжирования.");
    }
}