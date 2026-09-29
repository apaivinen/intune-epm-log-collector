using EpmLogCollector.Configuration;
using EpmLogCollector.Models.LogAnalytics;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace EpmLogCollector.Clients;

public sealed class LogsIngestionClient(
    ILogsIngestionTransport transport,
    IOptions<LogsIngestionOptions> options)
{
    private const int MaxRetryAttempts = 3;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task UploadAsync(
        IEnumerable<EpmElevationRequestLog> logs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logs);

        var records = logs as IReadOnlyCollection<EpmElevationRequestLog> ?? logs.ToArray();
        if (records.Count == 0)
        {
            return;
        }

        var ingestionOptions = options.Value;
        var batch = new List<BinaryData>();
        var payloadSizeBytes = 2;

        foreach (var record in records)
        {
            var serializedRecord = BinaryData.FromObjectAsJson(record, SerializerOptions);
            var recordSizeBytes = serializedRecord.ToMemory().Length;
            var separatorSizeBytes = batch.Count == 0 ? 0 : 1;

            if (recordSizeBytes + 2 > ingestionOptions.MaxBatchSizeBytes)
            {
                throw new InvalidDataException(
                    $"Elevation request '{record.ElevationRequestId ?? "(unknown)"}' exceeds the configured Logs Ingestion batch size.");
            }

            if (payloadSizeBytes + separatorSizeBytes + recordSizeBytes > ingestionOptions.MaxBatchSizeBytes)
            {
                await UploadBatchAsync(batch, ingestionOptions, cancellationToken);
                batch.Clear();
                payloadSizeBytes = 2;
                separatorSizeBytes = 0;
            }

            batch.Add(serializedRecord);
            payloadSizeBytes += separatorSizeBytes + recordSizeBytes;
        }

        await UploadBatchAsync(batch, ingestionOptions, cancellationToken);
    }

    private Task UploadBatchAsync(
        IReadOnlyCollection<BinaryData> batch,
        LogsIngestionOptions ingestionOptions,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return Task.CompletedTask;
        }

        return UploadBatchWithRetryAsync(batch, ingestionOptions, cancellationToken);
    }

    private async Task UploadBatchWithRetryAsync(
        IReadOnlyCollection<BinaryData> batch,
        LogsIngestionOptions ingestionOptions,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await transport.UploadAsync(
                    ingestionOptions.DataCollectionRuleImmutableId,
                    ingestionOptions.StreamName,
                    batch,
                    cancellationToken);
                return;
            }
            catch (LogsIngestionTransportException exception) when (
                IsTransientStatusCode(exception.StatusCode)
                && attempt < MaxRetryAttempts)
            {
                await Task.Delay(GetRetryDelay(exception.RetryAfter, attempt), cancellationToken);
            }
            catch (HttpRequestException) when (attempt < MaxRetryAttempts)
            {
                await Task.Delay(GetExponentialRetryDelay(attempt), cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested
                && attempt < MaxRetryAttempts)
            {
                await Task.Delay(GetExponentialRetryDelay(attempt), cancellationToken);
            }
        }
    }

    private static TimeSpan GetRetryDelay(TimeSpan? retryAfter, int attempt)
    {
        if (retryAfter is { } delay)
        {
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        return GetExponentialRetryDelay(attempt);
    }

    private static TimeSpan GetExponentialRetryDelay(int attempt)
    {
        return TimeSpan.FromSeconds(Math.Min(30, 1 << attempt));
    }

    private static bool IsTransientStatusCode(int statusCode)
    {
        return statusCode is 0 or 408 or 429 || statusCode is >= 500 and <= 599;
    }
}