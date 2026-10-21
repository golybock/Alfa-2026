namespace AlfaNews.Contracts.Domain;

public enum OcrStatus
{
	Queued,
	Processing,
	Ready,
	Partial,
	Failed
}