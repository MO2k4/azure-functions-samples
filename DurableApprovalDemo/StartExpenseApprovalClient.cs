using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace DurableApprovalDemo;

public static class StartExpenseApprovalClient
{
    [Function(nameof(StartExpenseApprovalClient))]
    public static async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "expenses")]
            HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        ExpenseReport report = await req.ReadFromJsonAsync<ExpenseReport>()
            ?? throw new InvalidOperationException("Expense report body is required.");

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(ExpenseApprovalOrchestrator), report);

        // 202 + management URLs; the orchestration is now parked on its WaitForExternalEvent.
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}

// Variant showing Option 2 from the article: make the instance ID the business key so
// the approval endpoint can reconstruct it from the route with no lookup at all.
public static class StartExpenseApprovalWithCustomIdClient
{
    [Function(nameof(StartExpenseApprovalWithCustomIdClient))]
    public static async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "expenses/custom")]
            HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        ExpenseReport report = await req.ReadFromJsonAsync<ExpenseReport>()
            ?? throw new InvalidOperationException("Expense report body is required.");

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(ExpenseApprovalOrchestrator),
            report,
            new StartOrchestrationOptions { InstanceId = $"expense-{report.ReportId}" });

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}
