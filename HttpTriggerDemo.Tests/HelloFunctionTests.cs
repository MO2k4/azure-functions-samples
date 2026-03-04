using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace HttpTriggerDemo.Tests;

// The isolated worker model + ASP.NET Core integration uses an HttpCoordinator that holds
// an incoming HTTP request until a matching gRPC invocation from the Functions host arrives.
// That design makes WebApplicationFactory unsuitable for in-process integration tests.
//
// The idiomatic approach: call function methods directly. They are plain C# methods that
// take ASP.NET Core types (HttpRequest, IActionResult), so you can test them without any
// Functions runtime infrastructure.
public class HelloFunctionTests
{
    private readonly HelloFunction _function =
        new(NullLogger<HelloFunction>.Instance);

    [Fact]
    public void Run_WithName_ReturnsPersonalizedGreeting()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?name=Alice");

        var result = _function.Run(context.Request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Hello, Alice!", ok.Value);
    }

    [Fact]
    public void Run_WithoutName_ReturnsFallbackGreeting()
    {
        var context = new DefaultHttpContext();

        var result = _function.Run(context.Request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Hello, world!", ok.Value);
    }
}
