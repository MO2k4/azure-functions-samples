using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DurableApprovalDemo;

// Activities are the only place I/O and side effects are allowed. The orchestrator
// hands the approver's decision here to settle or reject the report.
public static class SettleExpenseActivity
{
    [Function(nameof(SettleExpenseActivity))]
    public static string Run([ActivityTrigger] SettlementInput input)
    {
        var (report, decision) = input;
        if (!decision.Approved)
            return $"Report {report.ReportId} rejected by {decision.Approver}.";

        // Real work belongs here: queue the reimbursement, post to the ledger, notify the employee.
        return $"Report {report.ReportId} approved by {decision.Approver}; {report.Amount:C} scheduled for payment.";
    }
}

// Used by the escalation variant: notify the second approver (the manager's manager)
// before the orchestrator waits again. Minimal body; real work would send an email.
public static class NotifyEscalationApproverActivity
{
    [Function(nameof(NotifyEscalationApproverActivity))]
    public static void Run([ActivityTrigger] ExpenseReport report, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(NotifyEscalationApproverActivity));
        logger.LogInformation(
            "Escalating expense report {ReportId} ({Amount:C}) to the next approver.",
            report.ReportId, report.Amount);
    }
}
