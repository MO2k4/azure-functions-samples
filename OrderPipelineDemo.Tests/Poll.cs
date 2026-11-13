using Azure;

namespace OrderPipelineDemo.Tests;

public static class Poll
{
    public static Task WaitUntilAsync(Func<Task<Response<bool>>> condition, TimeSpan timeout) =>
        WaitUntilAsync(async () => (bool)await condition(), timeout);

    public static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Condition not met within {timeout}.");

            await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        }
    }
}
