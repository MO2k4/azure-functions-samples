using Microsoft.Extensions.Logging;
using OrderProcessor.Core.Models;

namespace OrderProcessor.Core.Validators;

public sealed class OrderValidator(ILogger<OrderValidator> logger)
{
    public ValidationResult Validate(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.OrderId))
        {
            return Reject(order, "OrderId is required");
        }

        if (string.IsNullOrWhiteSpace(order.CustomerId))
        {
            return Reject(order, "CustomerId is required");
        }

        if (order.Amount <= 0m)
        {
            return Reject(order, "Amount must be greater than zero");
        }

        return ValidationResult.Ok();
    }

    private ValidationResult Reject(Order order, string error)
    {
        logger.LogWarning("Order {OrderId} rejected: {Error}", order.OrderId, error);
        return ValidationResult.Fail(error);
    }
}

public readonly record struct ValidationResult(bool IsValid, string? Error)
{
    public static ValidationResult Ok() => new(true, null);
    public static ValidationResult Fail(string error) => new(false, error);
}
