using System.Text.Json;
using EpmLogCollector.Models.Graph;
using Xunit;

namespace EpmLogCollector.Tests;

public sealed class ElevationRequestTests
{
    [Fact]
    public void Deserialize_MapsGraphElevationRequestAndApplicationDetails()
    {
        const string json = """
            {
              "@odata.type": "#microsoft.graph.privilegeManagementElevationRequest",
              "id": "request-123",
              "requestedByUserId": "user-123",
              "requestedOnDeviceId": "device-123",
              "requestedByUserPrincipalName": "user@example.com",
              "deviceName": "device-01",
              "requestCreatedDateTime": "2025-01-02T03:04:05Z",
              "requestLastModifiedDateTime": "2025-01-02T03:14:05Z",
              "requestJustification": "Install approved software",
              "applicationDetail": {
                "@odata.type": "microsoft.graph.elevationRequestApplicationDetail",
                "fileHash": "sha256-value",
                "fileName": "setup.exe",
                "filePath": "C:\\Install\\setup.exe",
                "fileDescription": "Application installer",
                "publisherName": "Example Publisher",
                "publisherCert": "certificate-value",
                "productName": "Example Product",
                "productInternalName": "example",
                "productVersion": "1.2.3"
              },
              "status": "pending",
              "reviewCompletedByUserId": "reviewer-123",
              "reviewCompletedByUserPrincipalName": "admin@example.com",
              "reviewCompletedDateTime": "2025-01-02T03:15:00Z",
              "requestExpiryDateTime": "2025-01-03T03:04:05Z",
              "reviewerJustification": "Approved"
            }
            """;

        var request = JsonSerializer.Deserialize<ElevationRequest>(json);

        Assert.NotNull(request);
        Assert.Equal("#microsoft.graph.privilegeManagementElevationRequest", request.ODataType);
        Assert.Equal("request-123", request.Id);
        Assert.Equal("user-123", request.RequestedByUserId);
        Assert.Equal("device-123", request.RequestedOnDeviceId);
        Assert.Equal("user@example.com", request.RequestedByUserPrincipalName);
        Assert.Equal("device-01", request.DeviceName);
        Assert.Equal(DateTimeOffset.Parse("2025-01-02T03:04:05Z"), request.RequestCreatedDateTime);
        Assert.Equal(DateTimeOffset.Parse("2025-01-02T03:14:05Z"), request.RequestLastModifiedDateTime);
        Assert.Equal("Install approved software", request.RequestJustification);
        Assert.Equal("pending", request.Status);
        Assert.Equal("reviewer-123", request.ReviewCompletedByUserId);
        Assert.Equal("admin@example.com", request.ReviewCompletedByUserPrincipalName);
        Assert.Equal(DateTimeOffset.Parse("2025-01-02T03:15:00Z"), request.ReviewCompletedDateTime);
        Assert.Equal(DateTimeOffset.Parse("2025-01-03T03:04:05Z"), request.RequestExpiryDateTime);
        Assert.Equal("Approved", request.ReviewerJustification);

        Assert.NotNull(request.ApplicationDetail);
        Assert.Equal("microsoft.graph.elevationRequestApplicationDetail", request.ApplicationDetail.ODataType);
        Assert.Equal("sha256-value", request.ApplicationDetail.FileHash);
        Assert.Equal("setup.exe", request.ApplicationDetail.FileName);
        Assert.Equal("C:\\Install\\setup.exe", request.ApplicationDetail.FilePath);
        Assert.Equal("Application installer", request.ApplicationDetail.FileDescription);
        Assert.Equal("Example Publisher", request.ApplicationDetail.PublisherName);
        Assert.Equal("certificate-value", request.ApplicationDetail.PublisherCert);
        Assert.Equal("Example Product", request.ApplicationDetail.ProductName);
        Assert.Equal("example", request.ApplicationDetail.ProductInternalName);
        Assert.Equal("1.2.3", request.ApplicationDetail.ProductVersion);
    }

    [Fact]
    public void Deserialize_AllowsOptionalPropertiesToBeOmittedOrNull()
    {
        const string json = """
            {
              "id": "request-123",
              "requestCreatedDateTime": null,
              "applicationDetail": null
            }
            """;

        var request = JsonSerializer.Deserialize<ElevationRequest>(json);

        Assert.NotNull(request);
        Assert.Equal("request-123", request.Id);
        Assert.Null(request.RequestCreatedDateTime);
        Assert.Null(request.ApplicationDetail);
        Assert.Null(request.RequestedByUserId);
    }
}