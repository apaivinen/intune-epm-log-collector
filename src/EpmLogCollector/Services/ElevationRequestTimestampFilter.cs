using EpmLogCollector.Models.Graph;

namespace EpmLogCollector.Services;

public static class ElevationRequestTimestampFilter
{
    public static IReadOnlyList<ElevationRequest> FilterNewerThan(
        IEnumerable<ElevationRequest> requests,
        DateTimeOffset? watermark)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var filteredRequests = new List<ElevationRequest>();
        foreach (var request in requests)
        {
            var timestamp = GetTimestamp(request);
            if (watermark is null || timestamp > watermark.Value)
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