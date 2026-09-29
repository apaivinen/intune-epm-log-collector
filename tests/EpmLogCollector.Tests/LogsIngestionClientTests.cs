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

    [Fact]
    public async Task UploadAsync_RetriesTransientFailureAndHonorsRetryAfter()
    {
        var transport = new RecordingLogsIngestionTransport
        {
            FailureForCall = call => call == 1
                ? new LogsIngestionTransportException(429, TimeSpan.Zero, new HttpRequestException("Throttled."))
                : null
        };
        var client = CreateClient(transport, 100_000);
        var record = new EpmElevationRequestLog
        {
            TimeGenerated = DateTimeOffset.UtcNow,
            ElevationRequestId = "request-123",
            IngestionTime = DateTimeOffset.UtcNow
        };

        await client.UploadAsync([record]);

        Assert.Equal(2, transport.CallCount);
        Assert.Single(transport.Batches[0]);
        Assert.Single(transport.Batches[1]);
    }

    [Fact]
    public async Task UploadAsync_DoesNotRetryNonTransientTransportFailures()
    {
        var transport = new RecordingLogsIngestionTransport
        {
            FailureForCall = _ => new LogsIngestionTransportException(
                400,
                TimeSpan.Zero,
                new InvalidOperationException("Invalid request configuration."))
        };
        var client = CreateClient(transport, 100_000);
        var record = new EpmElevationRequestLog
        {
            TimeGenerated = DateTimeOffset.UtcNow,
            ElevationRequestId = "request-123",
            IngestionTime = DateTimeOffset.UtcNow
        };

        await Assert.ThrowsAsync<LogsIngestionTransportException>(() => client.UploadAsync([record]));

        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task UploadAsync_StopsAfterMaximumRetryAttempts()
    {
        var transport = new RecordingLogsIngestionTransport
        {
            FailureForCall = _ => new LogsIngestionTransportException(
                503,
                TimeSpan.Zero,
                new HttpRequestException("Service unavailable."))
        };
        var client = CreateClient(transport, 100_000);
        var record = new EpmElevationRequestLog
        {
            TimeGenerated = DateTimeOffset.UtcNow,
            ElevationRequestId = "request-123",
            IngestionTime = DateTimeOffset.UtcNow
        };

        await Assert.ThrowsAsync<LogsIngestionTransportException>(() => client.UploadAsync([record]));

        Assert.Equal(4, transport.CallCount);
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
        public Func<int, Exception?>? FailureForCall { get; init; }

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
            if (FailureForCall?.Invoke(CallCount) is { } exception)
            {
                throw exception;
            }

            return Task.CompletedTask;
        }
    }
}