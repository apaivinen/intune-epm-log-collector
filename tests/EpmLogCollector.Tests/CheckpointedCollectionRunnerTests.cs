using EpmLogCollector.Services;
using Xunit;

namespace EpmLogCollector.Tests;

public sealed class CheckpointedCollectionRunnerTests
{
    [Fact]
    public async Task RunAsync_RetryUsesPriorCheckpointAfterFailedRunThenAdvancesAfterSuccess()
    {
        var previousCheckpoint = DateTimeOffset.Parse("2026-09-29T12:00:00Z");
        var successfulRunTime = DateTimeOffset.Parse("2026-09-29T12:10:00Z");
        var checkpointService = new InMemoryCheckpointService(previousCheckpoint);
        var runner = new CheckpointedCollectionRunner(checkpointService, new FixedTimeProvider(successfulRunTime));
        var attemptCount = 0;

        await Assert.ThrowsAsync<HttpRequestException>(() => runner.RunAsync((checkpoint, _) =>
        {
            attemptCount++;
            Assert.Equal(previousCheckpoint, checkpoint);
            throw new HttpRequestException("Simulated ingestion failure.");
        }));

        Assert.Equal(previousCheckpoint, checkpointService.LastSuccessfulCollection);
        Assert.Equal(0, checkpointService.SaveCount);

        await runner.RunAsync((checkpoint, _) =>
        {
            attemptCount++;
            Assert.Equal(previousCheckpoint, checkpoint);
            return Task.CompletedTask;
        });

        Assert.Equal(2, attemptCount);
        Assert.Equal(successfulRunTime, checkpointService.LastSuccessfulCollection);
        Assert.Equal(1, checkpointService.SaveCount);
    }

    private sealed class InMemoryCheckpointService(DateTimeOffset? lastSuccessfulCollection) : ICheckpointService
    {
        public DateTimeOffset? LastSuccessfulCollection { get; private set; } = lastSuccessfulCollection;
        public int SaveCount { get; private set; }

        public Task<DateTimeOffset?> GetLastSuccessfulCollectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LastSuccessfulCollection);
        }

        public Task SaveLastSuccessfulCollectionAsync(
            DateTimeOffset lastSuccessfulCollection,
            CancellationToken cancellationToken = default)
        {
            LastSuccessfulCollection = lastSuccessfulCollection;
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}