using System.Globalization;
using Azure;
using Azure.Monitor.Ingestion;

namespace EpmLogCollector.Clients;

public sealed class AzureMonitorLogsIngestionTransport(
    Azure.Monitor.Ingestion.LogsIngestionClient client) : ILogsIngestionTransport
{
    public async Task UploadAsync(
        string dataCollectionRuleImmutableId,
        string streamName,
        IEnumerable<BinaryData> logs,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await client.UploadAsync(
                dataCollectionRuleImmutableId,
                streamName,
                logs,
                cancellationToken: cancellationToken);
        }
        catch (RequestFailedException exception) when (IsTransientStatusCode(exception.Status))
        {
            throw new LogsIngestionTransportException(
                exception.Status,
                GetRetryAfter(exception),
                exception);
        }
    }

    private static bool IsTransientStatusCode(int statusCode)
    {
        return statusCode is 0 or 408 or 429 || statusCode is >= 500 and <= 599;
    }

    private static TimeSpan? GetRetryAfter(RequestFailedException exception)
    {
        var response = exception.GetRawResponse();
        if (response is null || !response.Headers.TryGetValue("Retry-After", out var retryAfter))
        {
            return null;
        }

        if (int.TryParse(retryAfter, NumberStyles.None, CultureInfo.InvariantCulture, out var delaySeconds))
        {
            return TimeSpan.FromSeconds(Math.Max(0, delaySeconds));
        }

        if (DateTimeOffset.TryParse(
            retryAfter,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var retryAfterDate))
        {
            var delayUntilRetry = retryAfterDate - DateTimeOffset.UtcNow;
            return delayUntilRetry > TimeSpan.Zero ? delayUntilRetry : TimeSpan.Zero;
        }

        return null;
    }
}