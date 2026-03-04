using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace HttpTriggerDemo.Tests;

public class OrderFunctionTests
{
    private readonly OrderFunction _function =
        new(NullLogger<OrderFunction>.Instance);

    [Fact]
    public void CreateOrder_WithValidOrder_Returns201Created()
    {
        var context = new DefaultHttpContext();
        var order = new CreateOrderRequest("WIDGET-42", 3);

        var result = _function.CreateOrder(context.Request, order);

        var created = Assert.IsType<CreatedResult>(result);
        Assert.NotNull(created.Location);
        Assert.StartsWith("/orders/", created.Location);
    }

    [Fact]
    public void CreateOrder_EchoesOrderInResponseBody()
    {
        var context = new DefaultHttpContext();
        var order = new CreateOrderRequest("GADGET-7", 1);

        var result = _function.CreateOrder(context.Request, order);

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Equal(order, created.Value);
    }
}
