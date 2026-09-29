using System.Text.Json.Serialization;

namespace EpmLogCollector.Models.LogAnalytics;

public sealed class EpmElevationRequestLog
{
    public const string DefaultSource = "MicrosoftIntuneEPM";

    [JsonPropertyName("TimeGenerated")]
    public DateTimeOffset TimeGenerated { get; init; }

    [JsonPropertyName("ElevationRequestId")]
    public string? ElevationRequestId { get; init; }

    [JsonPropertyName("RequestCreatedDateTime")]
    public DateTimeOffset? RequestCreatedDateTime { get; init; }

    [JsonPropertyName("RequestLastModifiedDateTime")]
    public DateTimeOffset? RequestLastModifiedDateTime { get; init; }

    [JsonPropertyName("Status")]
    public string? Status { get; init; }

    [JsonPropertyName("RequestedByUserId")]
    public string? RequestedByUserId { get; init; }

    [JsonPropertyName("RequestedByUserPrincipalName")]
    public string? RequestedByUserPrincipalName { get; init; }

    [JsonPropertyName("RequestedOnDeviceId")]
    public string? RequestedOnDeviceId { get; init; }

    [JsonPropertyName("DeviceName")]
    public string? DeviceName { get; init; }

    [JsonPropertyName("RequestJustification")]
    public string? RequestJustification { get; init; }

    [JsonPropertyName("FileName")]
    public string? FileName { get; init; }

    [JsonPropertyName("FilePath")]
    public string? FilePath { get; init; }

    [JsonPropertyName("FileDescription")]
    public string? FileDescription { get; init; }

    [JsonPropertyName("FileHash")]
    public string? FileHash { get; init; }

    [JsonPropertyName("PublisherName")]
    public string? PublisherName { get; init; }

    [JsonPropertyName("ProductName")]
    public string? ProductName { get; init; }

    [JsonPropertyName("ProductInternalName")]
    public string? ProductInternalName { get; init; }

    [JsonPropertyName("ProductVersion")]
    public string? ProductVersion { get; init; }

    [JsonPropertyName("RequestExpiryDateTime")]
    public DateTimeOffset? RequestExpiryDateTime { get; init; }

    [JsonPropertyName("ReviewCompletedByUserId")]
    public string? ReviewCompletedByUserId { get; init; }

    [JsonPropertyName("ReviewCompletedByUserPrincipalName")]
    public string? ReviewCompletedByUserPrincipalName { get; init; }

    [JsonPropertyName("ReviewCompletedDateTime")]
    public DateTimeOffset? ReviewCompletedDateTime { get; init; }

    [JsonPropertyName("ReviewerJustification")]
    public string? ReviewerJustification { get; init; }

    [JsonPropertyName("IngestionTime")]
    public DateTimeOffset IngestionTime { get; init; }

    [JsonPropertyName("Source")]
    public string Source { get; init; } = DefaultSource;
}