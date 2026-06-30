using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;

namespace DurableApprovalDemo;

public static class SubmitApprovalClient
{
    [Function(nameof(SubmitApprovalClient))]
    public static async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "expenses/{instanceId}/decision")]
            HttpRequestData req,
        string instanceId,
        [DurableClient] DurableTaskClient client)
    {
        ApprovalRequest body = await req.ReadFromJsonAsync<ApprovalRequest>()
            ?? throw new InvalidOperationException("Approval body is required.");

        var decision = new ApprovalDecision(
            DecisionId: Guid.NewGuid().ToString("N"),
            Approved: body.Approved,
            Approver: body.Approver,
            Note: body.Note);

        // Raises the event the orchestrator is waiting on. Returns when the event is
        // enqueued, not when the orchestrator has consumed it.
        await client.RaiseEventAsync(instanceId, "ApprovalDecision", decision);

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}
