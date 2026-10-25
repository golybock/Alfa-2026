namespace AlfaNews.Contracts.Domain;

public enum RefreshStatus
{
    Idle,
    Queued,
    Processing,
    Failed
}