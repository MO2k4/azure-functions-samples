using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace DurableApprovalDemo;

// The canonical orchestrator: races the approval event against a durable timer so a
// report nobody touches cannot park forever. The simple non-timeout variant lives
// below under a different function name for contrast with the article's first snippet.
public static class ExpenseApprovalOrchestrator
{
    [Function(nameof(ExpenseApprovalOrchestrator))]
    public static async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var report = context.GetInput<ExpenseReport>()!;

        // Deadline comes off the orchestration clock, NOT DateTime.UtcNow. Part 1's
        // determinism rule: every replay must compute the same instant, and
        // CurrentUtcDateTime is frozen to the original execution time on replay.
        DateTime deadline = context.CurrentUtcDateTime.AddDays(3);

        using var cts = new CancellationTokenSource();
        Task<ApprovalDecision> approvalTask =
            context.WaitForExternalEvent<ApprovalDecision>("ApprovalDecision");
        Task timeoutTask = context.CreateTimer(deadline, cts.Token);

        Task winner = await Task.WhenAny(approvalTask, timeoutTask);

        if (winner == approvalTask)
        {
            // The approval landed first. Cancel the timer before moving on.
            cts.Cancel();
            return await context.CallActivityAsync<string>(
                nameof(SettleExpenseActivity),
                new SettlementInput(report, approvalTask.Result));
        }

        // The timer won: nobody decided in time. Fail closed by auto-rejecting.
        var timedOut = new ApprovalDecision(
            DecisionId: $"timeout-{report.ReportId}",
            Approved: false,
            Approver: "system (timeout)",
            Note: $"No decision by {deadline:o}.");
        return await context.CallActivityAsync<string>(
            nameof(SettleExpenseActivity), new SettlementInput(report, timedOut));
    }
}

// The simple version from the article's first snippet: an indefinite wait with no
// deadline. Kept under a distinct function name so both can register in one app.
public static class SimpleExpenseApprovalOrchestrator
{
    [Function(nameof(SimpleExpenseApprovalOrchestrator))]
    public static async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var report = context.GetInput<ExpenseReport>()!;

        // Suspends here until an "ApprovalDecision" event is raised for this instance.
        // No thread is held and no compute is billed while the orchestration waits.
        ApprovalDecision decision =
            await context.WaitForExternalEvent<ApprovalDecision>("ApprovalDecision");

        return await context.CallActivityAsync<string>(
            nameof(SettleExpenseActivity), new SettlementInput(report, decision));
    }
}

// The escalation variant from the timeout section: instead of failing closed, the
// timeout branch re-notifies a second approver and waits once more with a fresh timer.
public static class EscalatingExpenseApprovalOrchestrator
{
    [Function(nameof(EscalatingExpenseApprovalOrchestrator))]
    public static async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var report = context.GetInput<ExpenseReport>()!;

        using var cts = new CancellationTokenSource();
        Task<ApprovalDecision> approvalTask =
            context.WaitForExternalEvent<ApprovalDecision>("ApprovalDecision");
        Task timeoutTask = context.CreateTimer(context.CurrentUtcDateTime.AddDays(3), cts.Token);

        if (await Task.WhenAny(approvalTask, timeoutTask) == approvalTask)
        {
            cts.Cancel();
            return await context.CallActivityAsync<string>(
                nameof(SettleExpenseActivity),
                new SettlementInput(report, approvalTask.Result));
        }

        // Escalation variant for the timeout branch: re-notify, then wait once more.
        await context.CallActivityAsync(nameof(NotifyEscalationApproverActivity), report);

        using var escalationCts = new CancellationTokenSource();
        Task<ApprovalDecision> escalatedApproval =
            context.WaitForExternalEvent<ApprovalDecision>("ApprovalDecision");
        Task escalationTimeout =
            context.CreateTimer(context.CurrentUtcDateTime.AddDays(2), escalationCts.Token);

        if (await Task.WhenAny(escalatedApproval, escalationTimeout) == escalatedApproval)
        {
            escalationCts.Cancel();
            return await context.CallActivityAsync<string>(
                nameof(SettleExpenseActivity),
                new SettlementInput(report, escalatedApproval.Result));
        }

        // Still nothing after the second window: now fail closed.
        var timedOut = new ApprovalDecision(
            DecisionId: $"timeout-{report.ReportId}",
            Approved: false,
            Approver: "system (timeout)",
            Note: $"No decision after escalation.");
        return await context.CallActivityAsync<string>(
            nameof(SettleExpenseActivity), new SettlementInput(report, timedOut));
    }
}
