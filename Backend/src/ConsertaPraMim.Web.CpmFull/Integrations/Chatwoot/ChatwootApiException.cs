namespace AppMobileCPM.Integrations.Chatwoot;

public sealed class ChatwootApiException : Exception
{
    public ChatwootApiException(string message, int? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int? StatusCode { get; }
}
