using System;
using Azure;
using Azure.Storage.Blobs;

namespace EpmLogCollector.Services;

public sealed class BlobCheckpointStore(BlobContainerClient containerClient, string blobName) : ICheckpointBlobStore
{
    public async Task<BinaryData?> DownloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var download = await containerClient.GetBlobClient(blobName).DownloadContentAsync(cancellationToken);
            return download.Value.Content;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task UploadAsync(BinaryData content, CancellationToken cancellationToken = default)
    {
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await containerClient.GetBlobClient(blobName).UploadAsync(
            content,
            overwrite: true,
            cancellationToken: cancellationToken);
    }
}