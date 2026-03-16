using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace HttpTriggerDemo;

public class CleanupFunction(ILogger<CleanupFunction> logger)
{
    [Function(nameof(CleanupFunction))]
    public Task Run([TimerTrigger("%CLEANUP_SCHEDULE%")] TimerInfo timer)
    {
        if (timer.IsPastDue)
            logger.LogWarning("Timer is running late — a scheduled run was missed");

        logger.LogInformation("Running cleanup at {Time}", DateTime.UtcNow);

        return Task.CompletedTask;
    }
}
