using EpmLogCollector.Clients;
using EpmLogCollector.Configuration;
using EpmLogCollector.Models.LogAnalytics;
using Microsoft.Extensions.Options;
using Xunit;

namespace EpmLogCollector.Tests;

public sealed class LogsIngestionClientTests
{
    [Fact]
    public async Task UploadAsync_ForwardsDcrStreamAndRecords()
    {
        var transport = new RecordingLogsIngestionTransport();
        var client = CreateClient(transport);
        var records = new[]
        {
            new EpmElevationRequestLog
            {
                TimeGenerated = DateTimeOffset.Parse("2026-09-29T12:00:00Z"),
                ElevationRequestId = "request-123",
                IngestionTime = DateTimeOffset.Parse("2026-09-29T12:01:00Z")
            }
        };

        await client.UploadAsync(records);

        Assert.Equal("dcr-immutable-id", transport.DataCollectionRuleImmutableId);
        Assert.Equal("Custom-EpmElevationRequests", transport.StreamName);
        Assert.Same(records[0], Assert.Single(transport.Logs!));
    }

    [Fact]
    public async Task UploadAsync_DoesNotCallTransportForEmptyBatch()
    {
        var transport = new RecordingLogsIngestionTransport();
        var client = CreateClient(transport);

        await client.UploadAsync([]);

        Assert.Equal(0, transport.CallCount);
    }

    private static LogsIngestionClient CreateClient(RecordingLogsIngestionTransport transport)
    {
        return new LogsIngestionClient(
            transport,
            Options.Create(new LogsIngestionOptions
            {
                Endpoint = "https://monitor.example.com",
                DataCollectionRuleImmutableId = "dcr-immutable-id",
                StreamName = "Custom-EpmElevationRequests"
            }));
    }

    private sealed class RecordingLogsIngestionTransport : ILogsIngestionTransport
    {
        public string? DataCollectionRuleImmutableId { get; private set; }
        public string? StreamName { get; private set; }
        public IEnumerable<EpmElevationRequestLog>? Logs { get; private set; }
        public int CallCount { get; private set; }

        public Task UploadAsync(
            string dataCollectionRuleImmutableId,
            string streamName,
            IEnumerable<EpmElevationRequestLog> logs,
            CancellationToken cancellationToken = default)
        {
            DataCollectionRuleImmutableId = dataCollectionRuleImmutableId;
            StreamName = streamName;
            Logs = logs.ToArray();
            CallCount++;
            return Task.CompletedTask;
        }
    }
}