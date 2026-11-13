using System.Text.Json.Serialization;

namespace OrderPipelineDemo;

public record Order(string Id, string CustomerId, decimal Total, OrderStatus Status);

// Producers send the name ("Paid"); System.Text.Json reads enums as numbers unless told otherwise.
[JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>))]
public enum OrderStatus
{
    Pending,
    Paid,
}

public record Invoice(string OrderId, string CustomerId, decimal Amount);
