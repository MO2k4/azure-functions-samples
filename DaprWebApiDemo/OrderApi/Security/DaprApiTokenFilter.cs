using System.Security.Cryptography;
using System.Text;

namespace OrderApi.Security;

/// <summary>
/// Rejects calls that do not carry the sidecar's app API token.
/// </summary>
/// <remarks>
/// <para>
/// The token asymmetry is the part that catches people out. Two variables, one header name:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <c>DAPR_API_TOKEN</c> secures the app's <b>outbound</b> calls to its own sidecar. The SDK
/// reads that variable itself and attaches the header; nothing in this project handles it.
/// </description></item>
/// <item><description>
/// <c>APP_API_TOKEN</c> secures the sidecar's <b>inbound</b> calls into the app: service
/// invocation arriving from another app, pub/sub deliveries, input bindings. The runtime sends
/// it on the same <c>dapr-api-token</c> header, and checking it is entirely the app's problem.
/// Dapr's guidance is one sentence long: look for the header.
/// </description></item>
/// </list>
/// <para>
/// A class-based filter is where that check belongs rather than a lambda, because it needs
/// configuration and a logger, and because a group-level <c>AddEndpointFilter&lt;T&gt;</c>
/// applies it to every route in the group without repeating it per endpoint. ASP.NET Core
/// builds the filter once per endpoint through <c>ActivatorUtilities</c>, so constructor
/// injection works without registering the type.
/// </para>
/// <para>
/// It deliberately fails open when no token is configured. That is what makes a plain
/// <c>curl</c> against the app port work in development, and it matches the runtime's own
/// behaviour: with no <c>APP_API_TOKEN</c> set there is no token for the sidecar to send, so a
/// closed filter would reject Dapr itself. In anything that is not a laptop, set the variable.
/// </para>
/// </remarks>
public sealed class DaprApiTokenFilter : IEndpointFilter
{
    /// <summary>Header the runtime uses in both directions.</summary>
    private const string HeaderName = "dapr-api-token";

    private readonly byte[]? expectedToken;
    private readonly ILogger<DaprApiTokenFilter> logger;

    /// <summary>Reads the expected token once, at endpoint construction.</summary>
    /// <param name="configuration">Environment variables are already part of this.</param>
    /// <param name="logger">Where rejections are recorded.</param>
    public DaprApiTokenFilter(IConfiguration configuration, ILogger<DaprApiTokenFilter> logger)
    {
        var token = configuration["APP_API_TOKEN"];

        expectedToken = string.IsNullOrEmpty(token) ? null : Encoding.UTF8.GetBytes(token);
        this.logger = logger;

        if (expectedToken is null)
        {
            logger.LogWarning("APP_API_TOKEN is not set; inbound Dapr calls are not authenticated.");
        }
    }

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (expectedToken is null)
        {
            return await next(context);
        }

        var presented = context.HttpContext.Request.Headers[HeaderName];

        // Exactly one value. A repeated header is a caller trying something.
        if (presented.Count != 1 || !Matches(presented[0], expectedToken))
        {
            logger.LogWarning(
                "Rejected {Method} {Path}: missing or invalid dapr-api-token.",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path);

            return Results.Unauthorized();
        }

        return await next(context);
    }

    /// <summary>Length-independent, content-independent comparison time.</summary>
    private static bool Matches(string? candidate, byte[] expected)
    {
        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        // FixedTimeEquals is false for a length mismatch without leaking where the difference
        // is. A token is a shared secret, so the ordinary string comparison is the wrong tool.
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(candidate), expected);
    }
}
