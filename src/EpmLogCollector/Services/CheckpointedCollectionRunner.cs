namespace EpmLogCollector.Services;

public sealed class CheckpointedCollectionRunner(
    ICheckpointService checkpointService,
    TimeProvider timeProvider)
{
    public async Task RunAsync(
        Func<DateTimeOffset?, CancellationToken, Task> collectAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collectAsync);

        var lastSuccessfulCollection = await checkpointService.GetLastSuccessfulCollectionAsync(cancellationToken);
        await collectAsync(lastSuccessfulCollection, cancellationToken);
        await checkpointService.SaveLastSuccessfulCollectionAsync(timeProvider.GetUtcNow(), cancellationToken);
    }
}