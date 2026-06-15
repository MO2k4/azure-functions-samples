using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;

namespace DurableFunctionsDemo;

public static class OrderClient
{
    [Function(nameof(StartOrder))]
    public static async Task<HttpResponseData> StartOrder(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        var order = await req.ReadFromJsonAsync<OrderRequest>();

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(OrderOrchestrator), order);

        return client.CreateCheckStatusResponse(req, instanceId);
    }
}
