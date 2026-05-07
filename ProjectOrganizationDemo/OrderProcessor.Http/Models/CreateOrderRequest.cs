namespace OrderProcessor.Http.Models;

public sealed record CreateOrderRequest(string OrderId, string CustomerId, decimal Amount);
