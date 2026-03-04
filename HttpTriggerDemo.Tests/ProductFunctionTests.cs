using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace HttpTriggerDemo.Tests;

public class ProductFunctionTests
{
    // Anonymous types defined in another assembly are not accessible via `dynamic`.
    // Serialising to JSON and deserialising into a local record sidesteps that restriction
    // while still asserting on the exact values the function returns.
    private sealed record ProductBody(string Category, int? Id);

    private readonly ProductFunction _function =
        new(NullLogger<ProductFunction>.Instance);

    [Fact]
    public void GetProduct_WithCategoryAndId_ReturnsBoth()
    {
        var context = new DefaultHttpContext();

        var result = _function.GetProduct(context.Request, "electronics", 42);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Deserialize(ok.Value);
        Assert.Equal("electronics", body.Category);
        Assert.Equal(42, body.Id);
    }

    [Fact]
    public void GetProduct_WithCategoryOnly_ReturnsNullId()
    {
        var context = new DefaultHttpContext();

        var result = _function.GetProduct(context.Request, "books", null);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Deserialize(ok.Value);
        Assert.Equal("books", body.Category);
        Assert.Null(body.Id);
    }

    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private static ProductBody Deserialize(object? value)
    {
        var json = JsonSerializer.Serialize(value);
        return JsonSerializer.Deserialize<ProductBody>(json, _jsonOptions)!;
    }
}
