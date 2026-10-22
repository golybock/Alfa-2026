using System.Text.Json;
using AlfaNews.Contracts.Domain;
using AlfaNews.Core.Data.Entities;
using AlfaNews.Core.Profiles;

namespace AlfaNews.Tests;

public sealed class CompanyProfileMapperTests
{
    [Fact]
    public void MapsRawFeaturesWithoutRestoringMissingRevenueOrInventingProducts()
    {
        var company = new CompanyEntity
        {
            Id = "sample",
            MainOkvedCode = "01.11",
            Revenue = null,
            RawData = JsonSerializer.SerializeToElement(new Dictionary<string, string>
            {
                ["CL_PRODUCT_RKO_MARK"] = "1.0",
                ["CL_PRODUCT_LOA_MARK"] = "1.0",
                ["FACTORING_MARK"] = "1.0",
                ["CL_PRODUCT_DEP_MARK"] = "0.0",
                ["FTS_FLAG"] = "1.0",
                ["NEGATIVEMARK"] = "1.0",
                ["REVENUE_COMPANY"] = "1000",
                ["COMPANY_AGE_YEARS_GRADATION"] = " 10–15 лет ",
                ["ECO_MARK_LABEL"] = " ",
                ["FTS_COUNTRY_NAME"] = " Китай, Гонконг, китай, Новая   страна "
            })
        };
        var exposure = new PaymentExposure(0, 0, 0, new Dictionary<string, decimal>());
        var profile = CompanyProfileMapper.Map(company, exposure, exposure, "v1");
        Assert.Equal("01.11", profile.OkvedCode);
        Assert.Equal("01", profile.Okved2);
        Assert.Null(profile.Revenue);
        Assert.Equal("10–15 лет", profile.AgeBand);
        Assert.Null(profile.Lifecycle);
        Assert.Equal(["factoring", "loan", "rko"], profile.Products);
        Assert.Equal(["ГОНКОНГ", "КИТАЙ", "НОВАЯ СТРАНА"], profile.FtsCountries);
        Assert.False(CountryNames.IsKnown("НОВАЯ СТРАНА"));
        Assert.True(CountryNames.IsKnown("китай"));
    }

    [Fact]
    public void MapsMissingRawFieldsToNullsAndEmptyLists()
    {
        var company = new CompanyEntity { Id = "sample", MainOkvedCode = "64.19", RawData = JsonSerializer.SerializeToElement(new { }) };
        var exposure = new PaymentExposure(0, 0, 0, new Dictionary<string, decimal>());
        var profile = CompanyProfileMapper.Map(company, exposure, exposure, "v1");
        Assert.Null(profile.AgeBand);
        Assert.Null(profile.Lifecycle);
        Assert.Empty(profile.Products);
        Assert.Empty(profile.FtsCountries);
        Assert.Empty(CountryNames.Parse(null));
    }
}