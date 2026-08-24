using System.Text.Json;
using System.Text.Json.Serialization;
using OrderApi.Inventory;
using OrderApi.Orders;
using OrderApi.Orders.CancelOrder;
using OrderApi.Orders.ConfirmOrder;
using OrderApi.Orders.CreateOrder;

namespace OrderApi;

/// <summary>
/// Every type this service puts on a wire, resolved by generated code instead of reflection.
/// </summary>
/// <remarks>
/// <para>
/// <c>JsonSerializerDefaults.Web</c> on the attribute is what keeps the generated metadata in
/// step with ASP.NET Core's own defaults: camelCase names, case-insensitive reads, numbers
/// accepted from strings. Get that wrong and the mismatch only shows up at runtime, as a
/// property that silently stays null.
/// </para>
/// <para>
/// The context is wired into two independent serializers in <c>Program.cs</c>, and the two
/// wirings are deliberately different. Dapr's client gets
/// <c>UseJsonSerializationOptions(Default.Options)</c>, a hard swap: the only things it
/// serialises are state values and invocation payloads, and every one of them is listed below,
/// so there is nothing for a fallback to catch. Minimal API's options get the context
/// <i>inserted</i> at the front of the resolver chain, because the framework also serialises
/// types this file will never list: <c>ProblemDetails</c>, the bare strings from
/// <c>Results.BadRequest</c>, and whatever the next middleware decides to write.
/// </para>
/// <para>
/// Adding a type to a response and forgetting to add a <c>[JsonSerializable]</c> line here is
/// the failure mode to watch for. It is caught by the Dapr path immediately and papered over
/// by the reflection fallback on the HTTP path.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(CreateOrderRequest))]
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(OrderLine))]
[JsonSerializable(typeof(OutOfStockResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(ConfirmConflictResponse))]
[JsonSerializable(typeof(CancelOrderRequest))]
[JsonSerializable(typeof(CancelOrderResponse))]
[JsonSerializable(typeof(OrderCancellation))]
[JsonSerializable(typeof(CancelConflictResponse))]
[JsonSerializable(typeof(StockCheckRequest))]
[JsonSerializable(typeof(StockCheckResponse))]
[JsonSerializable(typeof(StockLine))]
[JsonSerializable(typeof(StockShortfall))]
[JsonSerializable(typeof(DaprErrorBody))]
public sealed partial class OrderApiJsonContext : JsonSerializerContext;
