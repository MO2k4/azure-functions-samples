using Azure.Storage.Blobs;
using Azure.Storage.Queues;

namespace OrderPipelineDemo.Tests;

public class OrderRoundTripTests(AzuriteFixture azurite) : IClassFixture<FuncHostFixture>
{
    private const string NoCoreTools = "Azure Functions Core Tools (func) is not on PATH";

    [Fact]
    public async Task Placed_order_comes_out_as_an_invoice_blob()
    {
        Assert.SkipUnless(FuncHostFixture.CoreToolsInstalled, NoCoreTools);
        var ct = TestContext.Current.CancellationToken;
        var order = new Order(Guid.NewGuid().ToString("N"), "c-7", 99.50m, OrderStatus.Paid);
        var invoices = new BlobContainerClient(azurite.GetConnectionString(), "invoices");

        var queue = OrderQueue.Create(azurite.GetConnectionString());
        await new OrderApi(queue).PlaceAsync(order);

        var invoice = invoices.GetBlobClient($"order-{order.Id}.json");
        await WaitUntilAsync(() => invoice.ExistsAsync(ct), TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Plain_text_message_ends_up_in_the_poison_queue()
    {
        Assert.SkipUnless(FuncHostFixture.CoreToolsInstalled, NoCoreTools);
        var ct = TestContext.Current.CancellationToken;
        var order = new Order(Guid.NewGuid().ToString("N"), "c-7", 99.50m, OrderStatus.Paid);
        var poison = new QueueClient(azurite.GetConnectionString(), "orders-poison");
        await poison.CreateIfNotExistsAsync(cancellationToken: ct);

        // The SDK default: MessageEncoding.None, so the trigger receives plain text.
        var plainText = new QueueClient(azurite.GetConnectionString(), "orders");
        await new OrderApi(plainText).PlaceAsync(order);

        await WaitUntilAsync(
            async () => (await poison.PeekMessagesAsync(32, ct)).Value.Any(m => m.Body.ToString().Contains(order.Id, StringComparison.Ordinal)),
            TimeSpan.FromSeconds(60));
    }
}
