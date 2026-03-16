using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace HttpTriggerDemo.Tests;

public class OrderFunctionTests
{
    private readonly IOrderService _orderService = Substitute.For<IOrderService>();
    private readonly OrderFunction _function;

    public OrderFunctionTests()
    {
        _function = new OrderFunction(_orderService);
    }

    [Fact]
    public async Task CreateOrder_WhenServiceSucceeds_Returns201Created()
    {
        var request = new CreateOrderRequest("WIDGET-42", 3);
        var order = new Order("ORD-ABCD1234", "WIDGET-42", 3);
        _orderService.CreateOrderAsync(request).Returns(OrderResult.Success(order));

        var result = await _function.CreateOrder(new DefaultHttpContext().Request, request);

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Equal("/orders/ORD-ABCD1234", created.Location);
        Assert.Equal(order, created.Value);
    }

    [Fact]
    public async Task CreateOrder_WhenServiceFails_Returns400BadRequest()
    {
        var request = new CreateOrderRequest("WIDGET-42", -1);
        _orderService.CreateOrderAsync(request)
            .Returns(OrderResult.Failure("Quantity must be greater than zero"));

        var result = await _function.CreateOrder(new DefaultHttpContext().Request, request);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Quantity must be greater than zero", bad.Value);
    }
}
