using System.Text.Json.Serialization;

namespace OrdersApi;

[JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>))]
public enum OrderStatus
{
    Pending,
    Shipped,
    Cancelled,
}

public sealed record OrderLine(string Sku, int Quantity, decimal UnitPrice);

public sealed record CreateOrderRequest(string CustomerId, IReadOnlyList<OrderLine> Lines)
{
    public Dictionary<string, string[]> Validate()
    {
        Dictionary<string, string[]> errors = [];

        if (string.IsNullOrWhiteSpace(CustomerId))
            errors["customerId"] = ["A customer id is required."];

        if (Lines is not { Count: > 0 })
            errors["lines"] = ["An order needs at least one line."];
        else if (Lines.Any(line => line.Quantity < 1 || line.UnitPrice < 0 || string.IsNullOrWhiteSpace(line.Sku)))
            errors["lines"] = ["Every line needs a sku, a quantity of at least 1 and a non-negative unit price."];

        return errors;
    }
}

public sealed record Order(
    string OrderId,
    string CustomerId,
    IReadOnlyList<OrderLine> Lines,
    decimal Total,
    OrderStatus Status,
    DateTimeOffset CreatedAt);
