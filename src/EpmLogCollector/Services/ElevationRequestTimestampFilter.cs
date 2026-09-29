using EpmLogCollector.Configuration;
using EpmLogCollector.Models.Graph;
using Microsoft.Extensions.Options;

namespace EpmLogCollector.Services;

public sealed class ElevationRequestTimestampFilter(IOptions<CollectionOptions> options)
{
    public IReadOnlyList<ElevationRequest> FilterNewerThan(
        IEnumerable<ElevationRequest> requests,
        DateTimeOffset? watermark)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var overlapWindow = options.Value.OverlapWindow;
        if (overlapWindow < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The collection overlap window cannot be negative.");
        }

        var filterStart = watermark?.Subtract(overlapWindow);
        var filteredRequests = new List<ElevationRequest>();
        foreach (var request in requests)
        {
            var timestamp = GetTimestamp(request);
            if (filterStart is null
                || (overlapWindow == TimeSpan.Zero
                    ? timestamp > filterStart.Value
                    : timestamp >= filterStart.Value))
            {
                filteredRequests.Add(request);
            }
        }

        return filteredRequests;
    }

    private static DateTimeOffset GetTimestamp(ElevationRequest request)
    {
        var timestamp = request.RequestLastModifiedDateTime ?? request.RequestCreatedDateTime;
        return timestamp
            ?? throw new InvalidDataException(
                $"Elevation request '{request.Id ?? "(unknown)"}' has no creation or last-modified timestamp.");
    }
}