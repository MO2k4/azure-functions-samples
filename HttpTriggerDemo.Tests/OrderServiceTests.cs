using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace HttpTriggerDemo.Tests;

public class OrderServiceTests
{
    private readonly IOrderRepository _repository = Substitute.For<IOrderRepository>();
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        _service = new OrderService(NullLogger<OrderService>.Instance, _repository);
    }

    [Fact]
    public async Task CreateOrderAsync_WithValidRequest_ReturnsSuccess()
    {
        var request = new CreateOrderRequest("WIDGET-42", 3);

        var result = await _service.CreateOrderAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Order);
        Assert.Equal("WIDGET-42", result.Order.ProductId);
        Assert.Equal(3, result.Order.Quantity);
    }

    [Fact]
    public async Task CreateOrderAsync_WithValidRequest_SavesOrderToRepository()
    {
        var request = new CreateOrderRequest("WIDGET-42", 3);

        await _service.CreateOrderAsync(request);

        await _repository.Received(1).SaveAsync(Arg.Is<Order>(o =>
            o.ProductId == "WIDGET-42" && o.Quantity == 3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task CreateOrderAsync_WithInvalidQuantity_ReturnsFailure(int quantity)
    {
        var request = new CreateOrderRequest("WIDGET-42", quantity);

        var result = await _service.CreateOrderAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateOrderAsync_WithInvalidQuantity_DoesNotCallRepository(int quantity)
    {
        var request = new CreateOrderRequest("WIDGET-42", quantity);

        await _service.CreateOrderAsync(request);

        await _repository.DidNotReceive().SaveAsync(Arg.Any<Order>());
    }
}
