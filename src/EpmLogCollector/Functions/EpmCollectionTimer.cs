using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace EpmLogCollector.Functions;

public sealed class EpmCollectionTimer(ILogger<EpmCollectionTimer> logger)
{
    [Function(nameof(EpmCollectionTimer))]
    public void Run([TimerTrigger("%EpmCollectionSchedule%")] TimerInfo timerInfo)
    {
        logger.LogInformation(
            "EPM collection timer triggered. IsPastDue: {IsPastDue}, NextRun: {NextRun}",
            timerInfo.IsPastDue,
            timerInfo.ScheduleStatus?.Next);
    }
}