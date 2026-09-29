using EpmLogCollector.Models.Graph;
using EpmLogCollector.Services;
using EpmLogCollector.Configuration;
using Microsoft.Extensions.Options;
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

        var filtered = CreateFilter(TimeSpan.Zero).FilterNewerThan(requests, watermark);

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

        var filtered = CreateFilter(TimeSpan.Zero).FilterNewerThan([request], watermark);

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

        var filtered = CreateFilter(TimeSpan.FromMinutes(5)).FilterNewerThan(requests, null);

        Assert.Equal(requests, filtered);
    }

    [Fact]
    public void FilterNewerThan_RejectsRequestWithoutEitherTimestamp()
    {
        var request = new ElevationRequest { Id = "missing-timestamp" };

        Assert.Throws<InvalidDataException>(
            () => CreateFilter(TimeSpan.FromMinutes(5)).FilterNewerThan([request], null));
    }

    [Fact]
    public void FilterNewerThan_IncludesRequestsAtOrAfterTheOverlappedWatermark()
    {
        var watermark = DateTimeOffset.Parse("2026-09-29T12:00:00Z");
        var requests = new[]
        {
            new ElevationRequest { Id = "before-window", RequestCreatedDateTime = watermark.AddMinutes(-6) },
            new ElevationRequest { Id = "at-window-start", RequestCreatedDateTime = watermark.AddMinutes(-5) },
            new ElevationRequest { Id = "within-window", RequestCreatedDateTime = watermark.AddMinutes(-4) },
            new ElevationRequest { Id = "after-watermark", RequestCreatedDateTime = watermark.AddMinutes(1) }
        };

        var filtered = CreateFilter(TimeSpan.FromMinutes(5)).FilterNewerThan(requests, watermark);

        Assert.Equal(["at-window-start", "within-window", "after-watermark"], filtered.Select(request => request.Id));
    }

    [Fact]
    public void FilterNewerThan_RejectsNegativeOverlap()
    {
        var filter = CreateFilter(TimeSpan.FromMinutes(-1));
        var request = new ElevationRequest { Id = "request", RequestCreatedDateTime = DateTimeOffset.UtcNow };

        Assert.Throws<ArgumentOutOfRangeException>(() => filter.FilterNewerThan([request], DateTimeOffset.UtcNow));
    }

    private static ElevationRequestTimestampFilter CreateFilter(TimeSpan overlapWindow)
    {
        return new ElevationRequestTimestampFilter(Options.Create(new CollectionOptions
        {
            OverlapWindow = overlapWindow
        }));
    }
}