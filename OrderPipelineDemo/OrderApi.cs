using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Storage.Queues;

namespace OrderPipelineDemo;

public static class OrderQueue
{
    // The queue trigger expects Base64; the SDK default (None) sends plain text that the trigger can't decode.
    public static QueueClient Create(string connectionString) =>
        new(connectionString, "orders", new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
}

public sealed class OrderApi(QueueClient queue)
{
    // The public API contract writes enums as names, like most ASP.NET Core APIs configured for JSON.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Task PlaceAsync(Order order) => queue.SendMessageAsync(JsonSerializer.Serialize(order, Json));
}
