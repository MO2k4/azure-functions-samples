using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;

namespace DurableFanOutDemo;

public static class StartBatchClient
{
    [Function(nameof(StartBatchClient))]
    public static async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "batches")]
            HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        OrderItem[] items = await req.ReadFromJsonAsync<OrderItem[]>() ?? [];

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(ProcessBatchOrchestrator), items);

        // 202 Accepted + Location + the management URLs, without blocking on the result.
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}
