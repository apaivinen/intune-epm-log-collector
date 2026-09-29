using EpmLogCollector.Configuration;
using EpmLogCollector.Models.LogAnalytics;
using Microsoft.Extensions.Options;

namespace EpmLogCollector.Clients;

public sealed class LogsIngestionClient(
    ILogsIngestionTransport transport,
    IOptions<LogsIngestionOptions> options)
{
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
        await transport.UploadAsync(
            ingestionOptions.DataCollectionRuleImmutableId,
            ingestionOptions.StreamName,
            records,
            cancellationToken);
    }
}