using System.Globalization;
using AlfaNews.Contracts.Domain;

namespace AlfaNews.Core.Insights;

// Полоска «Почему это касается вас»: признаки компании, по которым событие её касается, в порядке причин отбора,
// и группа с известной выручкой участников. Признаки события (факторы) сюда не входят.
public static class CompanyTraits
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly IReadOnlyDictionary<string, string> Products = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["loan"] = "Кредит в банке",
        ["leasing"] = "Лизинг в банке",
        ["deposit"] = "Депозит в банке",
        ["guarantee"] = "Гарантия банка",
        ["payroll"] = "Зарплатный проект",
        ["factoring"] = "Факторинг в банке",
        ["rko"] = "Счёт в банке"
    };

    public static IReadOnlyList<CompanyTraitDto> Build(RelevanceDto relevance, CompanyProfile company, GroupProfile? group)
    {
        var traits = new List<CompanyTraitDto>();
        foreach (var reason in relevance.Reasons)
        {
            if (reason.Code.StartsWith("product_", StringComparison.Ordinal))
            {
                var product = reason.Code["product_".Length..];
                traits.Add(new CompanyTraitDto(reason.Code, Products.GetValueOrDefault(product, reason.Label ?? product)));
            }
            else if (reason.Code is "okved" or "country" or "cp" && reason.Label is { Length: > 0 } label)
                traits.Add(new CompanyTraitDto(reason.Code, label));
        }
        if (company.GroupId is not null)
            traits.Add(Group(group));
        return traits.DistinctBy(trait => trait.Code).ToArray();
    }

    private static CompanyTraitDto Group(GroupProfile? group) => group?.KnownRevenueSum is { } revenue and > 0
        ? new CompanyTraitDto("group", $"Группа · {Amount(revenue)}", revenue)
        : new CompanyTraitDto("group", "Группа компаний");

    public static string Amount(decimal rub) => rub switch
    {
        >= 1_000_000_000m => (rub / 1_000_000_000m).ToString("0.0", Russian) + " млрд ₽",
        >= 1_000_000m => (rub / 1_000_000m).ToString("0", Russian) + " млн ₽",
        _ => (rub / 1_000m).ToString("0", Russian) + " тыс. ₽"
    };
}
