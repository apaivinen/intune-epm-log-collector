using System.Text.Json.Serialization;

namespace EpmLogCollector.Models.Graph;

public sealed class GraphCollectionResponse<T>
{
    [JsonPropertyName("@odata.context")]
    public string? ODataContext { get; init; }

    [JsonPropertyName("@odata.nextLink")]
    public string? NextLink { get; init; }

    [JsonPropertyName("value")]
    public List<T>? Value { get; init; }
}