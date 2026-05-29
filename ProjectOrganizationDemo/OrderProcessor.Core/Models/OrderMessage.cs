namespace OrderProcessor.Core.Models;

public sealed record OrderMessage(string OrderId, string CustomerId, decimal Amount);
