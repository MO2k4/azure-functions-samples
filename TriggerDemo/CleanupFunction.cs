using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TriggerDemo;

public class CleanupFunction(ILogger<CleanupFunction> logger)
{
    [Function(nameof(CleanupFunction))]
    [FixedDelayRetry(5, "00:00:10")]
    public Task Run(
        [TimerTrigger("%CLEANUP_SCHEDULE%")] TimerInfo timer)
    {
        logger.LogInformation("Running daily cleanup at {Time}", DateTime.UtcNow);

        if (timer.IsPastDue)
        {
            logger.LogWarning("Timer is running late — a scheduled run was missed");
        }

        logger.LogInformation(
            "Next occurrence: {Next}",
            timer.ScheduleStatus?.Next);

        // Cleanup logic here
        return Task.CompletedTask;
    }
}
