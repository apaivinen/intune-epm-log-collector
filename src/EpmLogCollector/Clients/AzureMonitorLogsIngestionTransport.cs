using Azure.Monitor.Ingestion;
using EpmLogCollector.Models.LogAnalytics;

namespace EpmLogCollector.Clients;

public sealed class AzureMonitorLogsIngestionTransport(
    Azure.Monitor.Ingestion.LogsIngestionClient client) : ILogsIngestionTransport
{
    public async Task UploadAsync(
        string dataCollectionRuleImmutableId,
        string streamName,
        IEnumerable<EpmElevationRequestLog> logs,
        CancellationToken cancellationToken = default)
    {
        await client.UploadAsync(
            dataCollectionRuleImmutableId,
            streamName,
            logs,
            cancellationToken: cancellationToken);
    }
}