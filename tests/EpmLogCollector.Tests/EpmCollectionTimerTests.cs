using System.Reflection;
using EpmLogCollector.Functions;
using Microsoft.Azure.Functions.Worker;
using Xunit;

namespace EpmLogCollector.Tests;

public sealed class EpmCollectionTimerTests
{
    [Fact]
    public void Run_UsesConfigurableScheduleSetting()
    {
        var runMethod = typeof(EpmCollectionTimer).GetMethod(nameof(EpmCollectionTimer.Run));

        Assert.NotNull(runMethod);

        var trigger = runMethod.GetParameters()[0].GetCustomAttribute<TimerTriggerAttribute>();

        Assert.NotNull(trigger);
        Assert.Equal("%EpmCollectionSchedule%", trigger.Schedule);
    }
}