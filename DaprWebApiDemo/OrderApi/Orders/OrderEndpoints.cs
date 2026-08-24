using OrderApi.Orders.CancelOrder;
using OrderApi.Orders.ConfirmOrder;
using OrderApi.Orders.CreateOrder;
using OrderApi.Orders.GetOrder;
using OrderApi.Security;

namespace OrderApi.Orders;

/// <summary>
/// The one place that knows which order slices exist. Each slice owns its route, its contracts
/// and its handler; this file owns the prefix and the cross-cutting filter.
/// </summary>
public static class OrderEndpoints
{
    /// <summary>Maps every <c>/orders</c> route behind the inbound Dapr token check.</summary>
    /// <param name="app">The application's route builder.</param>
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // AddEndpointFilter on the group applies to every endpoint added to it afterwards, so
        // a slice cannot forget the token check by forgetting a line.
        var orders = app.MapGroup("/orders")
            .AddEndpointFilter<DaprApiTokenFilter>();

        orders.MapCreateOrder();
        orders.MapGetOrder();
        orders.MapConfirmOrder();
        orders.MapCancelOrder();
    }
}
