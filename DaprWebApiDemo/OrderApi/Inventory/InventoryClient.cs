using System.Text.Json;

namespace OrderApi.Inventory;

/// <summary>
/// The only place in orders-api that knows inventory-api exists. It owns the wire contract, the
/// error translation, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// The injected <see cref="HttpClient"/> comes from
/// <c>DaprClient.CreateInvokeHttpClient("inventory-api")</c> in <c>Program.cs</c>. That factory
/// is the supported service-invocation surface: <c>DaprClient.InvokeMethodAsync</c> and its
/// gRPC siblings have carried <c>[Obsolete]</c> since Dapr .NET SDK 1.17 ("Recommended
/// guidance is to use a native HTTP or gRPC client for service invocation"). The instance
/// method <c>daprClient.CreateInvokableHttpClient("inventory-api")</c> is equally current and
/// is a one-line delegation to the static one; it is the better choice when you already hold a
/// configured <see cref="Dapr.Client.DaprClient"/> and want its endpoint and API token reused
/// without repeating them.
/// </para>
/// <para>
/// The handler that factory installs rewrites the request URI and then passes the response
/// through untouched. It never inspects the status code and never throws. So every Dapr
/// failure arrives here as an ordinary <see cref="HttpResponseMessage"/> or an ordinary
/// <see cref="HttpRequestException"/>, and translating it is this class's job. The obsolete
/// path used to do it for you, throwing <c>InvocationException</c> with <c>.AppId</c>,
/// <c>.MethodName</c> and <c>.Response</c> attached; that richer exception is what a migration
/// off <c>InvokeMethodAsync</c> actually costs you.
/// </para>
/// </remarks>
/// <param name="http">Dapr-routed client; its BaseAddress is <c>http://inventory-api</c>.</param>
/// <param name="logger">Sink for the failure detail that must not reach the HTTP caller.</param>
public sealed class InventoryClient(
    [FromKeyedServices(InventoryClient.AppId)] HttpClient http,
    ILogger<InventoryClient> logger)
{
    /// <summary>
    /// Dapr app ID of the inventory service. This is the whole address: no host, no port, no
    /// service discovery configuration anywhere in this project.
    /// </summary>
    public const string AppId = "inventory-api";

    /// <summary>The error code the sidecar returns when it cannot route to an app ID.</summary>
    private const string DirectInvokeErrorCode = "ERR_DIRECT_INVOKE";

    /// <summary>Asks inventory-api whether every requested line can be fulfilled.</summary>
    /// <param name="request">The lines to check.</param>
    /// <param name="cancellationToken">Cancellation for the whole call.</param>
    /// <returns>The inventory answer, or a categorised failure.</returns>
    public async Task<Result<StockCheckResponse>> CheckStockAsync(
        StockCheckRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // A plain POST. "/stock/check" is the route on the target app; the app ID lives in
            // the BaseAddress, and the handler turns the pair into
            // {daprEndpoint}/v1.0/invoke/inventory-api/method/stock/check.
            using var response = await http.PostAsJsonAsync(
                "/stock/check",
                request,
                OrderApiJsonContext.Default.StockCheckRequest,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return await TranslateFailureAsync(response, cancellationToken);
            }

            var body = await response.Content.ReadFromJsonAsync(
                OrderApiJsonContext.Default.StockCheckResponse, cancellationToken);

            return body is null
                ? Fail(InvocationFailure.InvalidResponse, (int)response.StatusCode, "inventory-api returned an empty body.")
                : new Result<StockCheckResponse>.Success(body);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation, so the guard is what
            // separates "we gave up" from "the caller went away".
            logger.LogWarning(ex, "Stock check for {OrderId} timed out.", request.OrderId);

            return Fail(InvocationFailure.Timeout, null, "inventory-api did not answer in time.");
        }
        catch (HttpRequestException ex)
        {
            // Connection-level: daprd itself is not listening. Nothing was routed.
            logger.LogError(ex, "Could not reach the Dapr sidecar for a stock check on {OrderId}.", request.OrderId);

            return Fail(InvocationFailure.SidecarUnreachable, null, "The Dapr sidecar is not reachable.");
        }
    }

    /// <summary>
    /// Turns a non-success response into a failure case. The distinction that matters is
    /// whether the body carries a Dapr error code (the sidecar failed to route) or not (the
    /// target app answered and said no).
    /// </summary>
    private async Task<Result<StockCheckResponse>> TranslateFailureAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        var daprError = TryReadDaprError(raw);

        if (string.Equals(daprError?.ErrorCode, DirectInvokeErrorCode, StringComparison.Ordinal))
        {
            // HTTP 500 plus ERR_DIRECT_INVOKE means the sidecar has no address for the app ID:
            // "couldn't find service: inventory-api", or "timeout waiting for address for app
            // id inventory-api". Nothing ran on the far side, so this is retryable in a way an
            // ordinary 500 is not.
            logger.LogWarning(
                "Sidecar could not route to {AppId}: {Detail}", AppId, daprError?.Message ?? raw);

            return Fail(InvocationFailure.TargetUnreachable, status, $"Dapr could not route to '{AppId}'.");
        }

        logger.LogWarning("inventory-api answered {Status}: {Body}", status, raw);

        return Fail(InvocationFailure.UpstreamError, status, $"inventory-api answered {status}.");
    }

    /// <summary>Best-effort parse of the sidecar's JSON error envelope.</summary>
    private static DaprErrorBody? TryReadDaprError(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(raw, OrderApiJsonContext.Default.DaprErrorBody);
        }
        catch (JsonException)
        {
            // The target app is free to return whatever it likes on an error. Not being JSON
            // is a normal outcome here, not an exceptional one.
            return null;
        }
    }

    private static Result<StockCheckResponse> Fail(InvocationFailure kind, int? status, string message) =>
        new Result<StockCheckResponse>.Failure(new InvocationError(kind, status, message));
}

/// <summary>What the sidecar puts in the body when it fails a call itself.</summary>
/// <param name="ErrorCode">For example <c>ERR_DIRECT_INVOKE</c>.</param>
/// <param name="Message">Human-readable detail from the runtime.</param>
public sealed record DaprErrorBody(string? ErrorCode, string? Message);

/// <summary>One SKU and the quantity an order wants.</summary>
public sealed record StockLine(string Sku, int Quantity);

/// <summary>Request body for <c>POST /stock/check</c> on inventory-api.</summary>
public sealed record StockCheckRequest(string OrderId, IReadOnlyList<StockLine> Lines);

/// <summary>Inventory's answer.</summary>
public sealed record StockCheckResponse(bool Available, IReadOnlyList<StockShortfall> Shortfalls);

/// <summary>A line inventory cannot fulfil, and by how much.</summary>
public sealed record StockShortfall(string Sku, int Requested, int OnHand);
