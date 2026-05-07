namespace OrderProcessor.Queue.Models;

public sealed record OrderMessage(string OrderId, string CustomerId, decimal Amount);
