namespace OrderProcessor.Core.Models;

public sealed record Order(
    string OrderId,
    string CustomerId,
    decimal Amount,
    OrderStatus Status);
