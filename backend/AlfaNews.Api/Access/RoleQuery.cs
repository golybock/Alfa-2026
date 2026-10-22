using AlfaNews.Contracts.Domain;
using AlfaNews.Core.Access;

namespace AlfaNews.Api.Access;

public static class RoleQuery
{
    public static Role Parse(string? value) => value switch
    {
        "ceo" => Role.Ceo,
        "cfo" => Role.Cfo,
        _ => throw new AccessException(400, "invalid_input", "Укажите роль: ceo или cfo.")
    };
}