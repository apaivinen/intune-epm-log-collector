namespace EpmLogCollector.Clients;

public interface ILogsIngestionTransport
{
    Task UploadAsync(
        string dataCollectionRuleImmutableId,
        string streamName,
    IEnumerable<BinaryData> logs,
        CancellationToken cancellationToken = default);
}