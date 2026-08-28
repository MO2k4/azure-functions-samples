namespace DaprAspireDemo.OrderService.Inventory;

/// <summary>
/// The only place in order-service that knows inventory-service exists. It owns the wire contract
/// and the error translation, and nothing else.
/// </summary>
/// <remarks>
/// The injected <see cref="HttpClient"/> comes from
/// <c>DaprClient.CreateInvokeHttpClient("inventory-service")</c> in <c>Program.cs</c>. The handler
/// that factory installs rewrites the request URI and passes the response back untouched: it never
/// inspects the status code and never throws, so every Dapr failure arrives here as an ordinary
/// <see cref="HttpResponseMessage"/> or an ordinary <see cref="HttpRequestException"/>.
/// </remarks>
/// <param name="http">Dapr-routed client; its BaseAddress is <c>http://inventory-service</c>.</param>
/// <param name="logger">Sink for the failure detail that must not reach the HTTP caller.</param>
public sealed class InventoryClient(
    [FromKeyedServices(InventoryClient.AppId)] HttpClient http,
    ILogger<InventoryClient> logger)
{
    /// <summary>
    /// Dapr app ID of the inventory service, and the whole address: no host, no port, no service
    /// discovery configuration anywhere in this project. It has to match the
    /// <c>DaprSidecarOptions.AppId</c> the AppHost gives that project's sidecar.
    /// </summary>
    public const string AppId = "inventory-service";

    /// <summary>The error code the sidecar returns when it cannot route to an app ID.</summary>
    private const string DirectInvokeErrorCode = "ERR_DIRECT_INVOKE";

    /// <summary>Asks inventory-service whether every requested line can be fulfilled.</summary>
    /// <param name="request">The lines to check.</param>
    /// <param name="cancellationToken">Cancellation for the whole call.</param>
    /// <returns>The inventory answer, or a categorised failure.</returns>
    public async Task<Result<StockCheckResponse>> CheckStockAsync(
        StockCheckRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // A plain POST. "/stock/check" is the route on the target app; the app ID lives in the
            // BaseAddress, and the handler turns the pair into
            // {daprEndpoint}/v1.0/invoke/inventory-service/method/stock/check.
            using var response = await http.PostAsJsonAsync("/stock/check", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return await TranslateFailureAsync(response, cancellationToken);
            }

            StockCheckResponse? body;

            try
            {
                body = await response.Content.ReadFromJsonAsync<StockCheckResponse>(cancellationToken);
            }
            catch (System.Text.Json.JsonException ex)
            {
                // A 2xx is not a promise that the body is the shape this client expects. A
                // version-skewed or half-written response throws here, and without this clause it
                // leaves as an unhandled exception: the one failure the Result cannot describe.
                logger.LogWarning(ex, "inventory-service answered {Status} with a body that did not deserialise.", (int)response.StatusCode);

                return Fail(InvocationFailure.InvalidResponse, (int)response.StatusCode, "inventory-service returned a body that did not deserialise.");
            }

            return body is null
                ? Fail(InvocationFailure.InvalidResponse, (int)response.StatusCode, "inventory-service returned an empty body.")
                : new Result<StockCheckResponse>.Success(body);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation, so the guard is what separates
            // "we gave up" from "the caller went away".
            logger.LogWarning(ex, "Stock check for {OrderId} timed out.", request.OrderId);

            return Fail(InvocationFailure.Timeout, null, "inventory-service did not answer in time.");
        }
        catch (HttpRequestException ex)
        {
            // Connection-level: daprd itself is not listening, so nothing was routed. This is
            // what you get running this project on its own with `dotnet run` instead of through
            // the AppHost, or after stopping the order-service-dapr-cli resource in the dashboard.
            logger.LogError(ex, "Could not reach the Dapr sidecar for a stock check on {OrderId}.", request.OrderId);

            return Fail(InvocationFailure.SidecarUnreachable, null, "The Dapr sidecar is not reachable.");
        }
    }

    /// <summary>
    /// Turns a non-success response into a failure case. The distinction that matters is whether
    /// the body carries a Dapr error code (the sidecar failed to route) or not (the target app
    /// answered and said no).
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
            // HTTP 500 plus ERR_DIRECT_INVOKE means the sidecar has no address for the app ID.
            // During an Aspire start this is a normal transient: the target's sidecar may not have
            // registered with the placement service yet. It is permanent when something rewrote the
            // app ID before Dapr saw it; the detail then names an ID nobody registered, such as
            // "localhost". See the keyed-registration comment in Program.cs for the one way this
            // sample can produce that.
            logger.LogWarning("Sidecar could not route to {AppId}: {Detail}", AppId, daprError?.Message ?? raw);

            return Fail(InvocationFailure.TargetUnreachable, status, $"Dapr could not route to '{AppId}'.");
        }

        logger.LogWarning("inventory-service answered {Status}: {Body}", status, raw);

        return Fail(InvocationFailure.UpstreamError, status, $"inventory-service answered {status}.");
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
            return System.Text.Json.JsonSerializer.Deserialize<DaprErrorBody>(
                raw,
                System.Text.Json.JsonSerializerOptions.Web);
        }
        catch (System.Text.Json.JsonException)
        {
            // The target app is free to return whatever it likes on an error. Not being JSON is a
            // normal outcome here, not an exceptional one.
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

/// <summary>Request body for <c>POST /stock/check</c> on inventory-service.</summary>
public sealed record StockCheckRequest(string OrderId, IReadOnlyList<StockLine> Lines);

/// <summary>Inventory's answer.</summary>
public sealed record StockCheckResponse(bool Available, IReadOnlyList<StockShortfall> Shortfalls);

/// <summary>A line inventory cannot fulfil, and by how much.</summary>
public sealed record StockShortfall(string Sku, int Requested, int OnHand);
