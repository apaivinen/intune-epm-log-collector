namespace EpmLogCollector.Clients;

public sealed class LogsIngestionTransportException(
    int statusCode,
    TimeSpan? retryAfter,
    Exception innerException) : Exception(innerException.Message, innerException)
{
    public int StatusCode { get; } = statusCode;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}