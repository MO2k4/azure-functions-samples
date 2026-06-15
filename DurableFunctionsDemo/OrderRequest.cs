namespace DurableFunctionsDemo;

// DTO passed from the client into the orchestrator and on to each activity.
public record OrderRequest(string CustomerId, string Sku, int Quantity);
