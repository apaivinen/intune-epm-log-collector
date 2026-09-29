using EpmLogCollector.Models.Graph;
using EpmLogCollector.Services;
using Xunit;

namespace EpmLogCollector.Tests;

public sealed class ElevationRequestTimestampFilterTests
{
    [Fact]
    public void FilterNewerThan_UsesLastModifiedTimestampAndExcludesWatermarkBoundary()
    {
        var watermark = DateTimeOffset.Parse("2026-09-29T12:00:00Z");
        var requests = new[]
        {
            new ElevationRequest
            {
                Id = "created-before-but-updated-after",
                RequestCreatedDateTime = watermark.AddHours(-1),
                RequestLastModifiedDateTime = watermark.AddMinutes(1)
            },
            new ElevationRequest
            {
                Id = "exactly-at-watermark",
                RequestCreatedDateTime = watermark.AddHours(-1),
                RequestLastModifiedDateTime = watermark
            },
            new ElevationRequest
            {
                Id = "older-than-watermark",
                RequestCreatedDateTime = watermark.AddMinutes(-1),
                RequestLastModifiedDateTime = watermark.AddMinutes(-1)
            }
        };

        var filtered = ElevationRequestTimestampFilter.FilterNewerThan(requests, watermark);

        var request = Assert.Single(filtered);
        Assert.Equal("created-before-but-updated-after", request.Id);
    }

    [Fact]
    public void FilterNewerThan_UsesCreationTimestampWhenLastModifiedTimestampIsMissing()
    {
        var watermark = DateTimeOffset.Parse("2026-09-29T12:00:00Z");
        var request = new ElevationRequest
        {
            Id = "created-after-watermark",
            RequestCreatedDateTime = watermark.AddMinutes(1)
        };

        var filtered = ElevationRequestTimestampFilter.FilterNewerThan([request], watermark);

        Assert.Same(request, Assert.Single(filtered));
    }

    [Fact]
    public void FilterNewerThan_ReturnsAllTimestampedRequestsWhenWatermarkIsMissing()
    {
        var requests = new[]
        {
            new ElevationRequest { Id = "first", RequestCreatedDateTime = DateTimeOffset.UtcNow },
            new ElevationRequest { Id = "second", RequestCreatedDateTime = DateTimeOffset.UtcNow }
        };

        var filtered = ElevationRequestTimestampFilter.FilterNewerThan(requests, null);

        Assert.Equal(requests, filtered);
    }

    [Fact]
    public void FilterNewerThan_RejectsRequestWithoutEitherTimestamp()
    {
        var request = new ElevationRequest { Id = "missing-timestamp" };

        Assert.Throws<InvalidDataException>(
            () => ElevationRequestTimestampFilter.FilterNewerThan([request], null));
    }
}