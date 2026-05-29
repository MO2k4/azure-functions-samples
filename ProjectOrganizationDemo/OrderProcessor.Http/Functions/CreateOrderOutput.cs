using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using OrderProcessor.Core.Models;

namespace OrderProcessor.Http.Functions;

public sealed class CreateOrderOutput
{
    [HttpResult]
    public IActionResult HttpResponse { get; set; } = null!;

    [QueueOutput("orders")]
    public OrderMessage? OrderMessage { get; set; }
}
