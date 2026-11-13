using Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging.Abstractions;

namespace OrderPipelineDemo.Tests;

public class OrderProcessorStorageTests(AzuriteFixture azurite)
{
    private static readonly Order PaidOrder = new("A-42", "c-7", 99.50m, OrderStatus.Paid);

    [Fact]
    public async Task Run_writes_the_invoice_into_a_container_that_does_not_exist_yet()
    {
        var ct = TestContext.Current.CancellationToken;
        var invoices = new BlobContainerClient(
            azurite.GetConnectionString(), AzuriteFixture.UniqueName("invoices"));
        var processor = new OrderProcessor(invoices, NullLogger<OrderProcessor>.Instance);

        await processor.Run(PaidOrder, ct);

        var blob = await invoices.GetBlobClient("order-A-42.json").DownloadContentAsync(ct);
        Assert.Equal(99.50m, blob.Value.Content.ToObjectFromJson<Invoice>()!.Amount);
    }

    [Fact]
    public async Task Run_fails_on_an_uppercase_container_name()
    {
        var invoices = new BlobContainerClient(azurite.GetConnectionString(), "Invoices");
        var processor = new OrderProcessor(invoices, NullLogger<OrderProcessor>.Instance);

        var error = await Assert.ThrowsAsync<RequestFailedException>(
            () => processor.Run(PaidOrder, TestContext.Current.CancellationToken));

        Assert.Equal("InvalidResourceName", error.ErrorCode);
    }
}
