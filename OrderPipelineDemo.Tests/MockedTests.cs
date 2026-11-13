using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace OrderPipelineDemo.Tests;

// Both tests pass whether or not the four bugs in this sample are present.
public class MockedTests
{
    private static readonly Order PaidOrder = new("42", "c-7", 99.50m, OrderStatus.Paid);

    [Fact]
    public async Task PlaceAsync_sends_the_order_to_the_queue()
    {
        var queue = Substitute.For<QueueClient>();

        await new OrderApi(queue).PlaceAsync(PaidOrder);

#pragma warning disable xUnit1051 // Received() checks a recorded call; there is nothing to cancel
        await queue.Received().SendMessageAsync(Arg.Any<string>());
#pragma warning restore xUnit1051
    }

    [Fact]
    public async Task Run_uploads_the_invoice()
    {
        var invoices = Substitute.For<BlobContainerClient>();
        var blob = Substitute.For<BlobClient>();
        invoices.GetBlobClient(Arg.Any<string>()).Returns(blob);

        var processor = new OrderProcessor(invoices, NullLogger<OrderProcessor>.Instance);
        await processor.Run(PaidOrder, TestContext.Current.CancellationToken);

        await blob.Received().UploadAsync(Arg.Any<BinaryData>(), true, Arg.Any<CancellationToken>());
    }
}
