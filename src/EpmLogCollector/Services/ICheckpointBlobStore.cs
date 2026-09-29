namespace EpmLogCollector.Services;

public interface ICheckpointBlobStore
{
    Task<BinaryData?> DownloadAsync(CancellationToken cancellationToken = default);

    Task UploadAsync(BinaryData content, CancellationToken cancellationToken = default);
}