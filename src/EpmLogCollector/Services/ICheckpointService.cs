namespace EpmLogCollector.Services;

public interface ICheckpointService
{
    Task<DateTimeOffset?> GetLastSuccessfulCollectionAsync(CancellationToken cancellationToken = default);

    Task SaveLastSuccessfulCollectionAsync(
        DateTimeOffset lastSuccessfulCollection,
        CancellationToken cancellationToken = default);
}