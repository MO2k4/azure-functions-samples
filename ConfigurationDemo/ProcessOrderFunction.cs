using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace ConfigurationDemo;

public class ProcessOrderFunction(IOptions<ApiOptions> options)
{
    private readonly ApiOptions _api = options.Value;

    [Function("ProcessOrder")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(_api.TimeoutSeconds) };
        using var response = await client.GetAsync(new Uri($"{_api.BaseUrl}/orders"));
        response.EnsureSuccessStatusCode();
        return new OkResult();
    }
}
