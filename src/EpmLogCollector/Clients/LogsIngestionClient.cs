using EpmLogCollector.Configuration;
using EpmLogCollector.Models.LogAnalytics;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace EpmLogCollector.Clients;

public sealed class LogsIngestionClient(
    ILogsIngestionTransport transport,
    IOptions<LogsIngestionOptions> options)
{
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

        return transport.UploadAsync(
            ingestionOptions.DataCollectionRuleImmutableId,
            ingestionOptions.StreamName,
            batch,
            cancellationToken);
    }
}