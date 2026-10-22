using AlfaNews.Contracts.Domain;

namespace AlfaNews.Core.Profiles;

public static class ProfileReportBuilder
{
    public static ProfileBuildReport Create(string datasetVersion, string dataVersion, string algorithmVersion,
        IReadOnlyList<CompanyProfile> companies, IReadOnlyList<GroupProfile> groups)
    {
        var byGroup = groups.ToDictionary(group => group.GroupId, StringComparer.Ordinal);
        var ranked = companies.Where(company => company.Okved2.Length == 2
                && (company.GroupId is not null && byGroup.TryGetValue(company.GroupId, out var group)
                    ? group.KnownRevenueSum >= 15000000000m : company.Revenue >= 15000000000m))
            .OrderByDescending(company => company.Products.Count)
            .ThenByDescending(company => company.FtsCountries.Count > 0)
            .ThenByDescending(company => Math.Max(company.Incoming.Coverage, company.Outgoing.Coverage))
            .ThenByDescending(company => Math.Max(company.Incoming.TotalExternalAmount, company.Outgoing.TotalExternalAmount))
            .ThenBy(company => company.CompanyId, StringComparer.Ordinal).ToArray();
        var sectors = ranked.GroupBy(company => company.Okved2).Take(2)
            .Select(sector => new DemoSectorCandidates(sector.Key, sector.Take(3).Select(company =>
            {
                var group = company.GroupId is null ? null : byGroup.GetValueOrDefault(company.GroupId);
                var limitations = new List<string>();
                if (company.Revenue is null)
                    limitations.Add("Выручка компании неизвестна.");
                if (group is null)
                    limitations.Add("Нет данных о группе.");
                else if (group.RevenueKnownCount < group.MemberIds.Count)
                    limitations.Add("Выручка части доступных участников группы неизвестна.");
                if (company.Incoming.TotalExternalAmount == 0)
                    limitations.Add("Нет наблюдаемого входящего платёжного объёма.");
                if (company.Outgoing.TotalExternalAmount == 0)
                    limitations.Add("Нет наблюдаемого исходящего платёжного объёма.");
                if (company.Incoming.Coverage < 1 || company.Outgoing.Coverage < 1)
                    limitations.Add("Отрасли части контрагентов неизвестны либо нет платёжного объёма.");
                return new DemoCandidate(company, group, limitations);
            }).ToArray())).ToArray();
        var drafts = new List<SensitivityDraft>();
        foreach (var sector in sectors)
        {
            drafts.Add(new SensitivityDraft(sector.Okved2, "Спрос в отрасли", null,
                "Возможное изменение спроса на деятельность компаний этой отрасли.",
                "Проверить конкретный канал на новых публикациях и подтвердить применимость к выбранной компании.",
                null, null, "draft"));
            if (sector.Candidates.Any(candidate => candidate.Company.Products.Any(product => product is "loan" or "leasing" or "guarantee" or "factoring")))
                drafts.Add(new SensitivityDraft(sector.Okved2, "Ключевая ставка", "key_rate",
                    "При наличии соответствующего продукта возможен канал стоимости нового финансирования.",
                    "Уточнить условия продукта и зависимость ставки договора от ключевой; текущая долговая нагрузка неизвестна.",
                    null, null, "draft"));
            if (sector.Candidates.Any(candidate => candidate.Company.Products.Contains("deposit")))
                drafts.Add(new SensitivityDraft(sector.Okved2, "Ключевая ставка", "key_rate",
                    "При депозитном продукте возможен канал доходности нового размещения средств.",
                    "Проверить срок, доступный остаток и условия размещения: в CSV их нет.", null, null, "draft"));
            if (sector.Candidates.Any(candidate => candidate.Company.FtsCountries.Count > 0))
                drafts.Add(new SensitivityDraft(sector.Okved2, "Внешнеторговые изменения", null,
                    "Проверить применимость события к странам, указанным в данных ФТС.",
                    "Направление торговли, валюты и конкретные операции не установлены данными профиля.", null, null, "draft"));
        }
        var unmapped = companies.SelectMany(company => company.FtsCountries).Where(country => !CountryNames.IsKnown(country))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return new ProfileBuildReport(datasetVersion, dataVersion, algorithmVersion, companies.Count, groups.Count,
            sectors, drafts, unmapped,
            [
                "Профили отражают доступную CLIENT-выборку; фактический состав групп может быть неполным.",
                "Сумма выручки группы учитывает только известные значения доступных участников; выручка относится к 2025 году.",
                "Платежи наблюдаются за 01.01–31.10.2026 в рублях; дат отдельных операций нет.",
                "Флаг продукта не определяет общий долг, валюту, ставку договора или инвестиционные планы компании.",
                "Страны ФТС не определяют направление торговли; неизвестные названия сохраняются без выдуманных ISO-кодов.",
                "Платёжная связь не устанавливает характер отношений компаний.",
                "Кандидаты выбраны по данным профилей; актуальные новости и покрытие источниками ещё нужно проверить.",
                "Черновики каналов не являются утверждённой чувствительностью; веса и знак требуют проверки K04 и утверждения Артёма."
            ]);
    }
}