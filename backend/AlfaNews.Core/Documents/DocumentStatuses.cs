namespace AlfaNews.Core.Documents;

// Документ «ждёт ключа модели»: он failed, и все его страницы не распознаны только из-за отсутствия ключа. После настройки
// ключа страницы можно повторить. Отдельный флаг, а не значение OcrStatus: в базе статусы прежние (failed и last_error страницы).
public static class DocumentStatuses
{
    public const string KeyRequired = "llm_key_required";

    public static bool WaitsForKey(string status, IReadOnlyCollection<(string Status, string? LastError)> pages) =>
        status == "failed" && pages.Count > 0 &&
        pages.All(page => page.Status == "failed" && page.LastError == KeyRequired);
}