using System.Collections.Frozen;

namespace InventoryApi.Stock;

/// <summary>
/// The service-invocation target. There is no Dapr type in this file, and no Dapr package in
/// this project: being reachable as <c>inventory-api</c> is a property of how the process is
/// started, not of how it is written.
/// </summary>
public static class StockEndpoints
{
    /// <summary>Stand-in for a real inventory read model.</summary>
    private static readonly FrozenDictionary<string, int> OnHand = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["AZ-KEYBOARD"] = 12,
        ["AZ-MOUSE"] = 40,
        ["AZ-MONITOR"] = 3,
        ["AZ-DOCK"] = 0,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Maps the stock routes.</summary>
    /// <param name="app">The application's route builder.</param>
    public static void MapStockEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/stock/check", CheckStock).WithName("CheckStock");
        app.MapGet("/stock/{sku}", GetStock).WithName("GetStock");
    }

    /// <summary>
    /// Answers a whole order in one call. orders-api reaches this as
    /// <c>POST http://inventory-api/stock/check</c>; the sidecar turns that into a call to
    /// this process on its app port, so the handler sees an ordinary request.
    /// </summary>
    private static IResult CheckStock(StockCheckRequest request)
    {
        if (request.Lines.Count == 0)
        {
            return Results.BadRequest(new ErrorResponse("A stock check needs at least one line."));
        }

        var shortfalls = request.Lines
            .Select(line => new { line.Sku, line.Quantity, OnHand = Available(line.Sku) })
            .Where(line => line.OnHand < line.Quantity)
            .Select(line => new StockShortfall(line.Sku, line.Quantity, line.OnHand))
            .ToArray();

        return Results.Ok(new StockCheckResponse(shortfalls.Length == 0, shortfalls));
    }

    /// <summary>Single-SKU read, for poking at the service directly.</summary>
    private static IResult GetStock(string sku) =>
        Results.Ok(new StockLevel(sku, Available(sku)));

    private static int Available(string sku) => OnHand.GetValueOrDefault(sku, 0);
}

/// <summary>One SKU and the quantity an order wants.</summary>
public sealed record StockLine(string Sku, int Quantity);

/// <summary>Request body for <c>POST /stock/check</c>.</summary>
public sealed record StockCheckRequest(string OrderId, IReadOnlyList<StockLine> Lines);

/// <summary>The answer: available only when nothing is short.</summary>
public sealed record StockCheckResponse(bool Available, IReadOnlyList<StockShortfall> Shortfalls);

/// <summary>A line that cannot be fulfilled, and by how much.</summary>
public sealed record StockShortfall(string Sku, int Requested, int OnHand);

/// <summary>Response body for <c>GET /stock/{sku}</c>.</summary>
public sealed record StockLevel(string Sku, int OnHand);

/// <summary>A plain message body for validation failures.</summary>
public sealed record ErrorResponse(string Message);
