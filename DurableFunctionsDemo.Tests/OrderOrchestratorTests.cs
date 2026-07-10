using DurableFunctionsDemo;
using Microsoft.DurableTask;
using Moq;

namespace DurableFunctionsDemo.Tests;

// Unit tests mock TaskOrchestrationContext, stub each activity call, and invoke the
// orchestrator method directly. No engine runs and nothing replays: these prove
// branch and aggregation logic in milliseconds.
public class OrderOrchestratorTests
{
    [Fact]
    public async Task Valid_order_runs_to_confirmation()
    {
        var order = new OrderRequest(CustomerId: "cust-1", Sku: "sku-9", Quantity: 2);
        var context = new Mock<TaskOrchestrationContext>();

        context.Setup(x => x.GetInput<OrderRequest>()).Returns(order);
        context.Setup(x => x.CurrentUtcDateTime)
            .Returns(new DateTime(2026, 7, 17, 9, 0, 0, DateTimeKind.Utc));
        context.Setup(x => x.NewGuid())
            .Returns(Guid.Parse("00000000-0000-0000-0000-0000000000ab"));

        context.Setup(x => x.CallActivityAsync<bool>(
            It.Is<TaskName>(n => n.Name == nameof(ValidateOrderActivity)),
            It.IsAny<object>(), It.IsAny<TaskOptions>())).ReturnsAsync(true);
        context.Setup(x => x.CallActivityAsync<string>(
            It.Is<TaskName>(n => n.Name == nameof(CreateOrderActivity)),
            It.IsAny<object>(), It.IsAny<TaskOptions>())).ReturnsAsync("ORD-cust-1-sku-9");
        context.Setup(x => x.CallActivityAsync(
            It.Is<TaskName>(n => n.Name == nameof(SendConfirmationActivity)),
            It.IsAny<object>(), It.IsAny<TaskOptions>())).Returns(Task.CompletedTask);

        string result = await OrderOrchestrator.RunOrchestrator(context.Object);

        Assert.StartsWith("ORD-cust-1-sku-9", result);
    }

    [Fact]
    public async Task Invalid_order_stops_before_creation()
    {
        var order = new OrderRequest("cust-1", Sku: "", Quantity: 0);
        var context = new Mock<TaskOrchestrationContext>();
        context.Setup(x => x.GetInput<OrderRequest>()).Returns(order);
        context.Setup(x => x.CurrentUtcDateTime).Returns(DateTime.UnixEpoch);
        context.Setup(x => x.NewGuid()).Returns(Guid.Empty);
        context.Setup(x => x.CallActivityAsync<bool>(
            It.Is<TaskName>(n => n.Name == nameof(ValidateOrderActivity)),
            It.IsAny<object>(), It.IsAny<TaskOptions>())).ReturnsAsync(false);

        string result = await OrderOrchestrator.RunOrchestrator(context.Object);

        Assert.Equal("Order validation failed", result);
        context.Verify(x => x.CallActivityAsync<string>(
            It.Is<TaskName>(n => n.Name == nameof(CreateOrderActivity)),
            It.IsAny<object>(), It.IsAny<TaskOptions>()), Times.Never);
    }
}
