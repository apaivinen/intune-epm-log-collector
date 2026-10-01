using EpmLogCollector.Clients;
using EpmLogCollector.Models.Graph;
using EpmLogCollector.Models.LogAnalytics;
using EpmLogCollector.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace EpmLogCollector.Functions;

public sealed class EpmCollectionTimer(
    ILogger<EpmCollectionTimer> logger,
    CheckpointedCollectionRunner collectionRunner,
    GraphClient graphClient,
    ElevationRequestTimestampFilter timestampFilter,
    LogsIngestionClient logsIngestionClient,
    TimeProvider timeProvider)
{
    [Function(nameof(EpmCollectionTimer))]
    public async Task Run(
        [TimerTrigger("%EpmCollectionSchedule%")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "EPM collection timer triggered. IsPastDue: {IsPastDue}, NextRun: {NextRun}",
            timerInfo.IsPastDue,
            timerInfo.ScheduleStatus?.Next);

        await collectionRunner.RunAsync(
            async (watermark, token) =>
            {
                var elevationRequests = await graphClient.GetElevationRequestsAsync(token);
                var newRequests = timestampFilter.FilterNewerThan(elevationRequests, watermark);

                logger.LogInformation(
                    "Retrieved {TotalCount} elevation request(s); {NewCount} are new since the last checkpoint.",
                    elevationRequests.Count,
                    newRequests.Count);

                var logs = newRequests.Select(ToLogRecord);
                await logsIngestionClient.UploadAsync(logs, token);
            },
            cancellationToken);
    }

    private EpmElevationRequestLog ToLogRecord(ElevationRequest request) => new()
    {
        TimeGenerated = timeProvider.GetUtcNow(),
        ElevationRequestId = request.Id,
        RequestCreatedDateTime = request.RequestCreatedDateTime,
        RequestLastModifiedDateTime = request.RequestLastModifiedDateTime,
        Status = request.Status,
        RequestedByUserId = request.RequestedByUserId,
        RequestedByUserPrincipalName = request.RequestedByUserPrincipalName,
        RequestedOnDeviceId = request.RequestedOnDeviceId,
        DeviceName = request.DeviceName,
        RequestJustification = request.RequestJustification,
        FileName = request.ApplicationDetail?.FileName,
        FilePath = request.ApplicationDetail?.FilePath,
        FileDescription = request.ApplicationDetail?.FileDescription,
        FileHash = request.ApplicationDetail?.FileHash,
        PublisherName = request.ApplicationDetail?.PublisherName,
        ProductName = request.ApplicationDetail?.ProductName,
        ProductInternalName = request.ApplicationDetail?.ProductInternalName,
        ProductVersion = request.ApplicationDetail?.ProductVersion,
        RequestExpiryDateTime = request.RequestExpiryDateTime,
        ReviewCompletedByUserId = request.ReviewCompletedByUserId,
        ReviewCompletedByUserPrincipalName = request.ReviewCompletedByUserPrincipalName,
        ReviewCompletedDateTime = request.ReviewCompletedDateTime,
        ReviewerJustification = request.ReviewerJustification,
        IngestionTime = timeProvider.GetUtcNow()
    };
}