using EpmLogCollector.Models.LogAnalytics;

namespace EpmLogCollector.Clients;

public interface ILogsIngestionTransport
{
    Task UploadAsync(
        string dataCollectionRuleImmutableId,
        string streamName,
        IEnumerable<EpmElevationRequestLog> logs,
        CancellationToken cancellationToken = default);
}