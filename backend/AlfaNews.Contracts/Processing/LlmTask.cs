namespace AlfaNews.Contracts.Processing;

public enum LlmTask
{
    NewsExtract,
    NewsTriage,
    Plan,
    Draft,
    Repair,
    PdfOcr,
    Critic,
    Chat,
    ScopeGate
}