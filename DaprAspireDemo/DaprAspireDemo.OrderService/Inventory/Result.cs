namespace DaprAspireDemo.OrderService.Inventory;

/// <summary>
/// A closed two-case result. The private constructor means <see cref="Success"/> and
/// <see cref="Failure"/> are the only possible cases, so a <c>switch</c> over them is genuinely
/// exhaustive rather than exhaustive-by-convention.
/// </summary>
/// <typeparam name="TValue">The payload carried by a successful call.</typeparam>
public abstract record Result<TValue>
{
    private Result()
    {
    }

    /// <summary>The call completed and the response body deserialised.</summary>
    /// <param name="Value">The deserialised response.</param>
    public sealed record Success(TValue Value) : Result<TValue>;

    /// <summary>The call did not produce a usable response.</summary>
    /// <param name="Error">What went wrong, and whether it is worth retrying.</param>
    public sealed record Failure(InvocationError Error) : Result<TValue>;
}

/// <summary>Why a service invocation did not produce a usable response.</summary>
/// <param name="Kind">The category the caller branches on.</param>
/// <param name="StatusCode">The HTTP status the sidecar returned, when there was one.</param>
/// <param name="Message">Text safe enough to log; not necessarily safe to return to a caller.</param>
public sealed record InvocationError(InvocationFailure Kind, int? StatusCode, string Message);

/// <summary>
/// The failure categories that matter to a caller. They are deliberately coarser than HTTP status
/// codes, because the interesting question is "retry, give up, or blame the payload".
/// </summary>
public enum InvocationFailure
{
    /// <summary>
    /// The sidecar could not route to the target app ID: HTTP 500 with <c>ERR_DIRECT_INVOKE</c>.
    /// Transient right after an Aspire start, permanent if the app ID is misspelled.
    /// </summary>
    TargetUnreachable,

    /// <summary>
    /// The app could not reach its own sidecar at all. Nothing was routed anywhere: daprd is not
    /// running, or DAPR_HTTP_PORT points somewhere else.
    /// </summary>
    SidecarUnreachable,

    /// <summary>
    /// The target application ran and answered with a non-success status of its own. This is the
    /// target's error, not Dapr's, and retrying it is usually pointless.
    /// </summary>
    UpstreamError,

    /// <summary>The call succeeded but the body was missing or did not deserialise.</summary>
    InvalidResponse,

    /// <summary>The call did not finish inside the client's own deadline.</summary>
    Timeout,
}
