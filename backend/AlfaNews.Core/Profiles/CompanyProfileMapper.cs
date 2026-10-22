using System.Text.Json;
using AlfaNews.Contracts.Domain;
using AlfaNews.Core.Data.Entities;

namespace AlfaNews.Core.Profiles;

public static class CompanyProfileMapper
{
    private static readonly (string Field, string Product)[] ProductFields =
    [
        ("CL_PRODUCT_RKO_MARK", "rko"), ("CL_PRODUCT_GAR_MARK", "guarantee"),
        ("CL_PRODUCT_LOA_MARK", "loan"), ("CL_PRODUCT_LEA_MARK", "leasing"),
        ("CL_PRODUCT_DEP_MARK", "deposit"), ("CL_PRODUCT_ZPP_MARK", "payroll"), ("FACTORING_MARK", "factoring")
    ];

    public static CompanyProfile Map(CompanyEntity company, PaymentExposure incoming, PaymentExposure outgoing, string dataVersion)
    {
        var okved = company.MainOkvedCode.Trim();
        var okved2 = okved.Length >= 2 && char.IsAsciiDigit(okved[0]) && char.IsAsciiDigit(okved[1]) ? okved[..2] : "";
        var products = ProductFields.Where(field => RawValue(company.RawData, field.Field) == "1.0")
            .Select(field => field.Product).Order(StringComparer.Ordinal).ToArray();
        return new CompanyProfile(company.Id, company.GroupId, okved, okved2, company.MainOkvedName,
            company.Region, company.City, RawValue(company.RawData, "COMPANY_AGE_YEARS_GRADATION"),
            RawValue(company.RawData, "ECO_MARK_LABEL"), company.Revenue, products,
            CountryNames.Parse(RawValue(company.RawData, "FTS_COUNTRY_NAME")), incoming, outgoing, dataVersion);
    }

    private static string? RawValue(JsonElement raw, string key) => raw.ValueKind == JsonValueKind.Object
        && raw.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? string.IsNullOrWhiteSpace(value.GetString()) ? null : value.GetString()!.Trim()
            : null;
}