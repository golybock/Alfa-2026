namespace AlfaNews.Core.Profiles;

public static class CountryNames
{
    public const string Version = "ru-countries-v1";

    private static readonly HashSet<string> KnownNames = new(
        ("АБХАЗИЯ|АВСТРИЯ|АЗЕРБАЙДЖАН|АЛЖИР|АРГЕНТИНА|БАХРЕЙН|БЕЛАРУСЬ|БЕЛЬГИЯ|БОЛГАРИЯ|БРАЗИЛИЯ|" +
         "ВЕЛИКОБРИТАНИЯ|ВЕНГРИЯ|ВЬЕТНАМ|ГАБОН|ГАНА|ГЕРМАНИЯ|ГОНКОНГ|ГРЕЦИЯ|ГРУЗИЯ|ДАНИЯ|ЕГИПЕТ|" +
         "ИЗРАИЛЬ|ИНДИЯ|ИНДОНЕЗИЯ|ИРАК|ИРАН|ИСПАНИЯ|ИТАЛИЯ|КАЗАХСТАН|КАМБОДЖА|КАМЕРУН|КАТАР|КЕНИЯ|" +
         "КИПР|КИРГИЗИЯ|КИТАЙ|КОНГО|ЛАТВИЯ|ЛИВАН|ЛИВИЯ|ЛИТВА|МАКАО|МАЛАЙЗИЯ|МАЛЬДИВЫ|МАРОККО|" +
         "МЕКСИКА|МОЛДОВА|МОНГОЛИЯ|МЬЯНМА|НЕПАЛ|НИГЕРИЯ|НИДЕРЛАНДЫ|ОАЭ|ОМАН|ПАКИСТАН|ПЕРУ|ПОЛЬША|" +
         "ПОРТУГАЛИЯ|РОССИЯ|РУАНДА|СЕРБИЯ|СИНГАПУР|СЛОВЕНИЯ|США|ТАДЖИКИСТАН|ТАИЛАНД|ТАЙВАНЬ|ТАНЗАНИЯ|" +
         "ТУНИС|ТУРКМЕНИСТАН|ТУРЦИЯ|УЗБЕКИСТАН|ФИНЛЯНДИЯ|ФРАНЦИЯ|ЧЕХИЯ|ЧИЛИ|ШВЕЙЦАРИЯ|ШВЕЦИЯ|" +
         "ШРИ-ЛАНКА|ЭКВАДОР|ЭСТОНИЯ|ЭФИОПИЯ|ЮЖНАЯ КОРЕЯ|ЮЖНАЯ ОСЕТИЯ|ЯПОНИЯ").Split('|'), StringComparer.Ordinal);

    public static IReadOnlyList<string> Values => KnownNames.Order(StringComparer.Ordinal).ToArray();

    public static string Normalize(string name) =>
        string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    public static bool IsKnown(string name) => KnownNames.Contains(Normalize(name));

    public static IReadOnlyList<string> Parse(string? names) => (names ?? "").Split(',')
        .Select(Normalize).Where(name => name.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
        .ToArray();
}