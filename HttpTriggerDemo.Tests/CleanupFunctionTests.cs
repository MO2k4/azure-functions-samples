using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;

namespace HttpTriggerDemo.Tests;

// TimerInfo is a concrete, settable class from the Functions SDK.
// No mocking needed — construct it directly and set IsPastDue as required.
public class CleanupFunctionTests
{
    private readonly CleanupFunction _function =
        new(NullLogger<CleanupFunction>.Instance);

    [Fact]
    public async Task Run_WhenOnSchedule_CompletesWithoutError()
    {
        var timer = new TimerInfo { IsPastDue = false };

        await _function.Run(timer);

        // No exception thrown = the function handled the timer correctly.
        // Timer functions have no return value — the observable outcome is
        // either successful completion or an exception.
    }

    [Fact]
    public async Task Run_WhenPastDue_StillCompletes()
    {
        var timer = new TimerInfo { IsPastDue = true };

        await _function.Run(timer);
    }
}
