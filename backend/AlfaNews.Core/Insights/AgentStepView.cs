using System.Text.Json;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Core.Data.Entities;

namespace AlfaNews.Core.Insights;

// Шаги агента для интерфейса: человеческое название, итог и длительность, без аргументов, результатов и промптов.
public static class AgentStepView
{
    private const string ErrorMarker = " Код: ";

    public static AgentStepDto ToDto(AgentStepEntity step)
    {
        var found = step.EvidenceRefs.ValueKind == JsonValueKind.Array ? step.EvidenceRefs.GetArrayLength() : 0;
        var title = (step.Stage, step.ToolName) switch
        {
            ("plan", _) => "Составили план проверки",
            (_, AgentTools.EventTool) => "Разобрали новость",
            (_, "get_company_profile") => "Посмотрели профиль компании",
            (_, "get_payment_exposure") => "Посмотрели платежи компании",
            (_, "get_group_summary") => "Посмотрели группу компаний",
            (_, AgentTools.BankSearch) => "Искали позицию Альфа-Банка",
            (_, AgentTools.SimilarNews) => found > 0 ? $"Нашли похожие новости: {found}" : "Искали похожие новости",
            ("draft", _) => "Написали карточку, числа проверены кодом",
            ("critic", _) => "Проверка критиком",
            ("repair", _) => "Исправили по замечаниям критика",
            _ => "Шаг подготовки"
        };
        // Критик и починка пишут отказ в итог шага: «… Код: budget_exhausted.» и «Починка не прошла: …».
        var marker = step.Summary.IndexOf(ErrorMarker, StringComparison.Ordinal);
        var status = step.Stage switch
        {
            "critic" when marker >= 0 => AgentStepStatus.Skipped,
            "repair" when step.Summary.StartsWith("Починка не прошла", StringComparison.Ordinal) => AgentStepStatus
                .Failed,
            _ => AgentStepStatus.Done
        };
        var summary = marker >= 0 ? step.Summary[..marker] : step.Summary;
        return new AgentStepDto(step.Ordinal, step.Stage, title, summary, status, step.DurationMs);
    }
}