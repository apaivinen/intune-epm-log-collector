using System.Text.Json;

namespace EpmLogCollector.Services;

public sealed class BlobCheckpointService(ICheckpointBlobStore blobStore) : ICheckpointService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<DateTimeOffset?> GetLastSuccessfulCollectionAsync(
        CancellationToken cancellationToken = default)
    {
        var content = await blobStore.DownloadAsync(cancellationToken);
        if (content is null)
        {
            return null;
        }

        var checkpoint = content.ToObjectFromJson<CheckpointDocument>(SerializerOptions);
        if (checkpoint?.LastSuccessfulCollection is not { } timestamp)
        {
            throw new InvalidDataException("The collection checkpoint does not contain lastSuccessfulCollection.");
        }

        return timestamp;
    }

    public Task SaveLastSuccessfulCollectionAsync(
        DateTimeOffset lastSuccessfulCollection,
        CancellationToken cancellationToken = default)
    {
        var checkpoint = new CheckpointDocument
        {
            LastSuccessfulCollection = lastSuccessfulCollection.ToUniversalTime()
        };

        return blobStore.UploadAsync(
            BinaryData.FromObjectAsJson(checkpoint, SerializerOptions),
            cancellationToken);
    }

    private sealed class CheckpointDocument
    {
        public DateTimeOffset? LastSuccessfulCollection { get; init; }
    }
}