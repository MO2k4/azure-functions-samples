using DurableFunctionsDemo;
using Moq;

namespace DurableFunctionsDemo.Tests;

// Activities run once per invocation with no history to reconstruct, so they test
// like any other method: call directly and assert on the return value. Where an
// activity has a dependency, inject a fake through the constructor.
public class ActivitiesTests
{
    [Fact]
    public void Validate_rejects_empty_sku()
    {
        Assert.False(ValidateOrderActivity.Run(new OrderRequest("cust-1", "", 1)));
        Assert.True(ValidateOrderActivity.Run(new OrderRequest("cust-1", "sku-9", 1)));
    }

    [Fact]
    public void Create_returns_order_id_from_input()
    {
        string id = CreateOrderActivity.Run(new OrderRequest("cust-1", "sku-9", 2));
        Assert.Equal("ORD-cust-1-sku-9", id);
    }

    [Fact]
    public async Task ChargeCard_delegates_to_gateway()
    {
        var gateway = new Mock<IPaymentGateway>();
        gateway.Setup(g => g.ChargeAsync("cust-1", 2)).ReturnsAsync(true);

        var activities = new PaymentActivities(gateway.Object);

        Assert.True(await activities.ChargeCard(new OrderRequest("cust-1", "sku-9", 2)));
        gateway.Verify(g => g.ChargeAsync("cust-1", 2), Times.Once);
    }
}
