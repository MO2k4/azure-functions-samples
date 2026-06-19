namespace DurableFanOutDemo;

// DTOs passed from the client into the orchestrator, on to each activity, and back
// out as the aggregated result. Records keep them immutable and value-compared.
public record OrderItem(string Sku, int Quantity);
public record OrderResult(string Sku, bool Reserved, decimal LineTotal);
public record BatchSummary(int Processed, int Reserved, decimal Total);
