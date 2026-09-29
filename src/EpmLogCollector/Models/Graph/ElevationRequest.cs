using System.Text.Json.Serialization;

namespace EpmLogCollector.Models.Graph;

public sealed class ElevationRequest
{
    [JsonPropertyName("@odata.type")]
    public string? ODataType { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("requestedByUserId")]
    public string? RequestedByUserId { get; init; }

    [JsonPropertyName("requestedOnDeviceId")]
    public string? RequestedOnDeviceId { get; init; }

    [JsonPropertyName("requestedByUserPrincipalName")]
    public string? RequestedByUserPrincipalName { get; init; }

    [JsonPropertyName("deviceName")]
    public string? DeviceName { get; init; }

    [JsonPropertyName("requestCreatedDateTime")]
    public DateTimeOffset? RequestCreatedDateTime { get; init; }

    [JsonPropertyName("requestLastModifiedDateTime")]
    public DateTimeOffset? RequestLastModifiedDateTime { get; init; }

    [JsonPropertyName("requestJustification")]
    public string? RequestJustification { get; init; }

    [JsonPropertyName("applicationDetail")]
    public ElevationRequestApplicationDetail? ApplicationDetail { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("reviewCompletedByUserId")]
    public string? ReviewCompletedByUserId { get; init; }

    [JsonPropertyName("reviewCompletedByUserPrincipalName")]
    public string? ReviewCompletedByUserPrincipalName { get; init; }

    [JsonPropertyName("reviewCompletedDateTime")]
    public DateTimeOffset? ReviewCompletedDateTime { get; init; }

    [JsonPropertyName("requestExpiryDateTime")]
    public DateTimeOffset? RequestExpiryDateTime { get; init; }

    [JsonPropertyName("reviewerJustification")]
    public string? ReviewerJustification { get; init; }
}

public sealed class ElevationRequestApplicationDetail
{
    [JsonPropertyName("@odata.type")]
    public string? ODataType { get; init; }

    [JsonPropertyName("fileHash")]
    public string? FileHash { get; init; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }

    [JsonPropertyName("filePath")]
    public string? FilePath { get; init; }

    [JsonPropertyName("fileDescription")]
    public string? FileDescription { get; init; }

    [JsonPropertyName("publisherName")]
    public string? PublisherName { get; init; }

    [JsonPropertyName("publisherCert")]
    public string? PublisherCert { get; init; }

    [JsonPropertyName("productName")]
    public string? ProductName { get; init; }

    [JsonPropertyName("productInternalName")]
    public string? ProductInternalName { get; init; }

    [JsonPropertyName("productVersion")]
    public string? ProductVersion { get; init; }
}