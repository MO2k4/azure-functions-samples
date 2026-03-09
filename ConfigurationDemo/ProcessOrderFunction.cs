using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace ConfigurationDemo;

public class ProcessOrderFunction(IHttpClientFactory httpClientFactory)
{
    [Function("ProcessOrder")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        var client = httpClientFactory.CreateClient("OrdersApi");
        var response = await client.GetAsync("/orders");
        response.EnsureSuccessStatusCode();
        return new OkResult();
    }
}
