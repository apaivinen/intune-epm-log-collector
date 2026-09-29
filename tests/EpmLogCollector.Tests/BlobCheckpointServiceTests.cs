using System.Text.Json;
using EpmLogCollector.Services;
using Xunit;

namespace EpmLogCollector.Tests;

public sealed class BlobCheckpointServiceTests
{
    [Fact]
    public async Task GetLastSuccessfulCollectionAsync_ReturnsNullWhenBlobIsMissing()
    {
        var service = new BlobCheckpointService(new InMemoryCheckpointBlobStore());

        var checkpoint = await service.GetLastSuccessfulCollectionAsync();

        Assert.Null(checkpoint);
    }

    [Fact]
    public async Task SaveLastSuccessfulCollectionAsync_StoresUtcTimestampAndCanReadItBack()
    {
        var blobStore = new InMemoryCheckpointBlobStore();
        var service = new BlobCheckpointService(blobStore);
        var expected = new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.FromHours(-4));

        await service.SaveLastSuccessfulCollectionAsync(expected);

        Assert.NotNull(blobStore.Content);
        using var json = JsonDocument.Parse(blobStore.Content.ToMemory());
        Assert.Equal("2026-09-29T16:00:00+00:00", json.RootElement.GetProperty("lastSuccessfulCollection").GetString());
        Assert.Equal(expected.ToUniversalTime(), await service.GetLastSuccessfulCollectionAsync());
    }

    [Fact]
    public async Task GetLastSuccessfulCollectionAsync_RejectsCheckpointWithoutTimestamp()
    {
        var blobStore = new InMemoryCheckpointBlobStore
        {
            Content = BinaryData.FromString("{}")
        };
        var service = new BlobCheckpointService(blobStore);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.GetLastSuccessfulCollectionAsync());
    }

    private sealed class InMemoryCheckpointBlobStore : ICheckpointBlobStore
    {
        public BinaryData? Content { get; set; }

        public Task<BinaryData?> DownloadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Content);
        }

        public Task UploadAsync(BinaryData content, CancellationToken cancellationToken = default)
        {
            Content = content;
            return Task.CompletedTask;
        }
    }
}