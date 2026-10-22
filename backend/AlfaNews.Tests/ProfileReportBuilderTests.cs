using AlfaNews.Contracts.Domain;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Profiles;

namespace AlfaNews.Tests;

public sealed class ProfileReportBuilderTests
{
    [Fact]
    public void KeepsSelectedCompaniesInDemoOrderWithTheirGroupsAndConditionalDrafts()
    {
        var a = Company("U_68157", "41.20.9") with { Products = ["loan", "guarantee", "leasing"] };
        var b = Company("U_71942", "16.10") with { Products = ["deposit"], FtsCountries = ["КИТАЙ"] };
        var extra = Company("U_12959", "62.01") with { Products = ["deposit"] };
        CompanyProfile[] companies = [extra, Company("unselected", "46.72"), b, a];
        var groups = companies.Select(company => new GroupProfile(company.GroupId!, [company.CompanyId], 20000000000m, 1, "v1")).ToArray();

        var report = ProfileReportBuilder.Create("dataset", "v1", "test", companies, groups);

        Assert.Equal(["41", "16", "62"], report.Sectors.Select(sector => sector.Okved2));
        var candidates = report.Sectors.SelectMany(sector => sector.Candidates).ToArray();
        Assert.Equal(["U_68157", "U_71942", "U_12959"], candidates.Select(candidate => candidate.Company.CompanyId));
        Assert.All(candidates, candidate =>
        {
            Assert.NotNull(candidate.Group);
            Assert.Equal(candidate.Company.GroupId, candidate.Group.GroupId);
        });
        Assert.Contains(candidates[2].Limitations, limitation => limitation.Contains("Дополнительная"));
        Assert.Contains(report.SensitivityDraft, draft => draft.Okved2 == "41" && draft.FactorCode == "key_rate");
        Assert.Contains(report.SensitivityDraft, draft => draft.Okved2 == "16" && draft.Channel.Contains("депозитном"));
        Assert.Contains(report.SensitivityDraft, draft => draft.Okved2 == "16" && draft.Topic == "Внешнеторговые изменения");
        Assert.All(report.SensitivityDraft, draft =>
        {
            Assert.Equal("draft", draft.Status);
            Assert.Null(draft.Weight);
            Assert.Null(draft.Sign);
        });
        Assert.Equal(4, report.CompanyCount);
        Assert.Equal(4, report.GroupCount);
    }

    [Fact]
    public void DoesNotSubstituteMissingDemoCompanies()
    {
        var report = ProfileReportBuilder.Create("dataset", "v1", "test", [Company("other", "41.20")], []);

        Assert.Empty(report.Sectors);
        Assert.Empty(report.SensitivityDraft);
        Assert.Equal(1, report.CompanyCount);
    }

    private static CompanyProfile Company(string id, string okved)
    {
        var company = new CompanyEntity { Id = id, GroupId = "group_" + id, MainOkvedCode = okved, Revenue = 1000000m };
        var exposure = new PaymentExposure(0, 0, 0, new Dictionary<string, decimal>());
        return CompanyProfileMapper.Map(company, exposure, exposure, "v1");
    }
}