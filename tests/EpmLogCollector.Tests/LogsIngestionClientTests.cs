using System.Text.Json;
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
        var client = CreateClient(transport, 100_000);
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
        var uploadedLog = Assert.Single(transport.Batches).Single().ToObjectFromJson<EpmElevationRequestLog>();
        Assert.Equal(records[0].ElevationRequestId, uploadedLog?.ElevationRequestId);
    }

    [Fact]
    public async Task UploadAsync_DoesNotCallTransportForEmptyBatch()
    {
        var transport = new RecordingLogsIngestionTransport();
        var client = CreateClient(transport, 100_000);

        await client.UploadAsync([]);

        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task UploadAsync_SplitsRecordsIntoBatchesWithinPayloadLimit()
    {
        var records = Enumerable.Range(1, 5)
            .Select(index => new EpmElevationRequestLog
            {
                TimeGenerated = DateTimeOffset.Parse("2026-09-29T12:00:00Z"),
                ElevationRequestId = $"request-{index}",
                IngestionTime = DateTimeOffset.Parse("2026-09-29T12:01:00Z")
            })
            .ToArray();
        var serializedRecordSize = BinaryData.FromObjectAsJson(records[0], new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .ToMemory().Length;
        var maxBatchSizeBytes = serializedRecordSize * 2 + 3;
        var transport = new RecordingLogsIngestionTransport();
        var client = CreateClient(transport, maxBatchSizeBytes);

        await client.UploadAsync(records);

        Assert.Equal([2, 2, 1], transport.Batches.Select(batch => batch.Count));
        Assert.All(transport.Batches, batch => Assert.True(GetJsonArraySize(batch) <= maxBatchSizeBytes));
        Assert.Equal(
            records.Select(record => record.ElevationRequestId),
            transport.Batches.SelectMany(batch => batch)
                .Select(record => record.ToObjectFromJson<EpmElevationRequestLog>()?.ElevationRequestId));
    }

    [Fact]
    public async Task UploadAsync_RejectsARecordLargerThanTheConfiguredPayloadLimit()
    {
        var transport = new RecordingLogsIngestionTransport();
        var client = CreateClient(transport, 100);
        var record = new EpmElevationRequestLog
        {
            TimeGenerated = DateTimeOffset.UtcNow,
            ElevationRequestId = "request-123",
            RequestJustification = new string('x', 1_000),
            IngestionTime = DateTimeOffset.UtcNow
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => client.UploadAsync([record]));

        Assert.Equal(0, transport.CallCount);
    }

    private static LogsIngestionClient CreateClient(RecordingLogsIngestionTransport transport, int maxBatchSizeBytes)
    {
        return new LogsIngestionClient(
            transport,
            Options.Create(new LogsIngestionOptions
            {
                Endpoint = "https://monitor.example.com",
                DataCollectionRuleImmutableId = "dcr-immutable-id",
                StreamName = "Custom-EpmElevationRequests",
                MaxBatchSizeBytes = maxBatchSizeBytes
            }));
    }

    private static int GetJsonArraySize(IReadOnlyCollection<BinaryData> batch)
    {
        return 2 + batch.Sum(item => item.ToMemory().Length) + Math.Max(0, batch.Count - 1);
    }

    private sealed class RecordingLogsIngestionTransport : ILogsIngestionTransport
    {
        public string? DataCollectionRuleImmutableId { get; private set; }
        public string? StreamName { get; private set; }
        public List<List<BinaryData>> Batches { get; } = [];
        public int CallCount { get; private set; }

        public Task UploadAsync(
            string dataCollectionRuleImmutableId,
            string streamName,
            IEnumerable<BinaryData> logs,
            CancellationToken cancellationToken = default)
        {
            DataCollectionRuleImmutableId = dataCollectionRuleImmutableId;
            StreamName = streamName;
            Batches.Add(logs.ToList());
            CallCount++;
            return Task.CompletedTask;
        }
    }
}