using Microsoft.Extensions.Logging;

namespace HttpTriggerDemo.Logging;

public static partial class OrderLogs
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Order {OrderId} created for {ProductId}")]
    public static partial void OrderCreated(ILogger logger, string orderId, string productId);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning,
        Message = "Order validation failed: {Reason}")]
    public static partial void OrderValidationFailed(ILogger logger, string reason);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information,
        Message = "Order saved to repository")]
    public static partial void OrderSaved(ILogger logger);
}
