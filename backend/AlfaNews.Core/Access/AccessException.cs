namespace AlfaNews.Core.Access;

public sealed class AccessException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}