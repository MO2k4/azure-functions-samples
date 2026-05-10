using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace HttpClientFactoryDemo;

public sealed class AuthorizePaymentFunction(
    IPaymentsApi payments,
    ILogger<AuthorizePaymentFunction> logger)
{
    [Function(nameof(AuthorizePaymentFunction))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "payments")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        var command = await request.ReadFromJsonAsync<AuthorizeCommand>(cancellationToken);
        if (command is null)
        {
            return new BadRequestObjectResult("Body is required");
        }

        var result = await payments.AuthorizeAsync(command, cancellationToken);

        logger.LogInformation("Authorized {OrderId} as {Reference}", command.OrderId, result.Reference);
        return new OkObjectResult(result);
    }
}
