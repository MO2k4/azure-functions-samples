namespace DurableApprovalDemo;

// DTOs passed from the start client into the orchestrator, raised back in as the
// approver's decision, and handed on to the settlement activity. Records keep them
// immutable and value-compared.
public record ExpenseReport(string ReportId, string Employee, decimal Amount, string Category);
public record ApprovalDecision(string DecisionId, bool Approved, string Approver, string? Note);
public record SettlementInput(ExpenseReport Report, ApprovalDecision Decision);

// The HTTP body the approval endpoint accepts. The server mints the DecisionId so
// every raised decision has a stable identity even if the platform delivers twice.
public record ApprovalRequest(bool Approved, string Approver, string? Note);
