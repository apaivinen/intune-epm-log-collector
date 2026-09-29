using System.Text.Json;
using EpmLogCollector.Models.LogAnalytics;
using Xunit;

namespace EpmLogCollector.Tests;

public sealed class EpmElevationRequestLogTests
{
    [Fact]
    public void Serialize_EmitsDcrColumnNamesInSchemaOrderAndDefaultsSource()
    {
        var generatedAt = DateTimeOffset.Parse("2026-09-29T12:00:00Z");
        var log = new EpmElevationRequestLog
        {
            TimeGenerated = generatedAt,
            ElevationRequestId = "request-123",
            RequestCreatedDateTime = generatedAt,
            RequestLastModifiedDateTime = generatedAt,
            Status = "pending",
            RequestedByUserId = "user-123",
            RequestedByUserPrincipalName = "user@example.com",
            RequestedOnDeviceId = "device-123",
            DeviceName = "device-01",
            RequestJustification = "Install approved software",
            FileName = "setup.exe",
            FilePath = "C:\\Install\\setup.exe",
            FileDescription = "Application installer",
            FileHash = "sha256-value",
            PublisherName = "Example Publisher",
            ProductName = "Example Product",
            ProductInternalName = "example",
            ProductVersion = "1.2.3",
            RequestExpiryDateTime = generatedAt.AddDays(1),
            ReviewCompletedByUserId = "reviewer-123",
            ReviewCompletedByUserPrincipalName = "admin@example.com",
            ReviewCompletedDateTime = generatedAt.AddMinutes(5),
            ReviewerJustification = "Approved",
            IngestionTime = generatedAt.AddMinutes(6)
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(log));
        var actualColumns = document.RootElement.EnumerateObject().Select(property => property.Name);

        Assert.Equal(
            [
                "TimeGenerated",
                "ElevationRequestId",
                "RequestCreatedDateTime",
                "RequestLastModifiedDateTime",
                "Status",
                "RequestedByUserId",
                "RequestedByUserPrincipalName",
                "RequestedOnDeviceId",
                "DeviceName",
                "RequestJustification",
                "FileName",
                "FilePath",
                "FileDescription",
                "FileHash",
                "PublisherName",
                "ProductName",
                "ProductInternalName",
                "ProductVersion",
                "RequestExpiryDateTime",
                "ReviewCompletedByUserId",
                "ReviewCompletedByUserPrincipalName",
                "ReviewCompletedDateTime",
                "ReviewerJustification",
                "IngestionTime",
                "Source"
            ],
            actualColumns);
        Assert.Equal(EpmElevationRequestLog.DefaultSource, log.Source);
        Assert.Equal(generatedAt, document.RootElement.GetProperty("TimeGenerated").GetDateTimeOffset());
    }
}