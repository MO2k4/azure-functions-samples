using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace OrderPipelineDemo;

public sealed class OrderProcessor(BlobContainerClient invoices, ILogger<OrderProcessor> logger)
{
    [Function(nameof(OrderProcessor))]
    public async Task Run([QueueTrigger("orders")] Order order, CancellationToken cancellationToken)
    {
        if (order.Status != OrderStatus.Paid)
            return;

        await invoices.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var invoice = new Invoice(order.Id, order.CustomerId, order.Total);
        await invoices.GetBlobClient($"order-{order.Id}.json")
            .UploadAsync(BinaryData.FromObjectAsJson(invoice), overwrite: true, cancellationToken);

        logger.LogInformation("Invoice written for order {OrderId}", order.Id);
    }
}
