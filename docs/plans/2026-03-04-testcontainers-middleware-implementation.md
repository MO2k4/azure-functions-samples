# Testcontainers Middleware Integration Tests Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add CorrelationIdMiddleware and ExceptionHandlingMiddleware to HttpTriggerDemo and verify them with Testcontainers integration tests that spin up the real Azure Functions host in Docker.

**Architecture:** Middleware is registered in the isolated worker pipeline via `UseMiddleware<T>()`. A Testcontainers fixture builds a Docker image from `HttpTriggerDemo/Dockerfile` on demand, starts the container, and exposes an `HttpClient` to tests. All tests share one container instance via an xUnit collection fixture.

**Tech Stack:** Azure Functions Isolated Worker (.NET 10), `IFunctionsWorkerMiddleware`, `Testcontainers` NuGet (v3.x), xUnit `IAsyncLifetime`, Docker

---

### Task 1: Switch all functions to anonymous auth

Changing auth level to `Anonymous` removes the need to pass `?code=` in integration tests. This is fine for a sample project.

**Files:**
- Modify: `HttpTriggerDemo/Hello.cs:12`
- Modify: `HttpTriggerDemo/ProductFunction.cs:12`
- Modify: `HttpTriggerDemo/OrderFunction.cs:15`

**Step 1: Update Hello.cs**

Change `AuthorizationLevel.Function` → `AuthorizationLevel.Anonymous`:

```csharp
[HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
```

**Step 2: Update ProductFunction.cs**

```csharp
[HttpTrigger(AuthorizationLevel.Anonymous, "get",
    Route = "products/{category:alpha}/{id:int?}")] HttpRequest req,
```

**Step 3: Update OrderFunction.cs**

```csharp
[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequest req,
```

**Step 4: Build to verify**

```bash
cd HttpTriggerDemo
dotnet build
```

Expected: no errors. The unit tests still pass because auth level is not exercised in direct-method tests.

**Step 5: Commit**

```bash
git add HttpTriggerDemo/Hello.cs HttpTriggerDemo/ProductFunction.cs HttpTriggerDemo/OrderFunction.cs
git commit -m "chore(samples): switch HTTP triggers to anonymous auth for integration testing"
```

---

### Task 2: Add CorrelationIdMiddleware

**Files:**
- Create: `HttpTriggerDemo/Middleware/CorrelationIdMiddleware.cs`

**Step 1: Create the file**

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace HttpTriggerDemo.Middleware;

public class CorrelationIdMiddleware : IFunctionsWorkerMiddleware
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();

        if (httpContext is null)
        {
            await next(context);
            return;
        }

        string correlationId = httpContext.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        httpContext.Response.Headers[HeaderName] = correlationId;

        await next(context);
    }
}
```

**Step 2: Build**

```bash
dotnet build HttpTriggerDemo
```

Expected: no errors.

---

### Task 3: Add ExceptionHandlingMiddleware and ErrorFunction

**Files:**
- Create: `HttpTriggerDemo/Middleware/ExceptionHandlingMiddleware.cs`
- Create: `HttpTriggerDemo/ErrorFunction.cs`

**Step 1: Create ExceptionHandlingMiddleware.cs**

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace HttpTriggerDemo.Middleware;

public class ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
    : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in function {FunctionName}",
                context.FunctionDefinition.Name);

            var httpContext = context.GetHttpContext();
            if (httpContext is not null)
            {
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                // Intentionally no body — do not leak exception details
            }
        }
    }
}
```

**Step 2: Create ErrorFunction.cs**

This function exists purely to give the ExceptionHandlingMiddleware something to catch.

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace HttpTriggerDemo;

public class ErrorFunction
{
    [Function("Error")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        throw new InvalidOperationException("Intentional error for middleware testing.");
    }
}
```

**Step 3: Build**

```bash
dotnet build HttpTriggerDemo
```

Expected: no errors.

---

### Task 4: Register middleware in Program.cs

**Files:**
- Modify: `HttpTriggerDemo/Program.cs`

**Step 1: Update Program.cs**

```csharp
using HttpTriggerDemo.Middleware;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.UseMiddleware<ExceptionHandlingMiddleware>(); // outermost — catches all
builder.UseMiddleware<CorrelationIdMiddleware>();     // innermost — per-request

builder.Build().Run();

// Expose the implicit Program type so integration test projects can reference it
// via WebApplicationFactory<Program>.
public partial class Program { }
```

**Step 2: Build and run unit tests**

```bash
dotnet build
dotnet test HttpTriggerDemo.Tests
```

Expected: all 6 existing unit tests pass (middleware registration does not affect direct-method tests).

**Step 3: Commit**

```bash
git add HttpTriggerDemo/Middleware/ HttpTriggerDemo/ErrorFunction.cs HttpTriggerDemo/Program.cs
git commit -m "feat(samples): add correlation ID and exception handling middleware"
```

---

### Task 5: Add Dockerfile

**Files:**
- Create: `HttpTriggerDemo/Dockerfile`

**Step 1: Create the Dockerfile**

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY HttpTriggerDemo.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0 AS runtime
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    AZURE_FUNCTIONS_ENVIRONMENT=Development \
    AzureWebJobsStorage=""
COPY --from=build /app/publish /home/site/wwwroot
```

> **Note on storage:** HTTP-only function apps can start without a real storage account. If the host refuses to start with `AzureWebJobsStorage=""`, change it to a valid Azurite connection string and add an Azurite container to the Testcontainers fixture.

**Step 2: Verify the Docker build manually**

```bash
cd HttpTriggerDemo
docker build -t httptriggerdemo-test .
```

Expected: build completes, image tagged `httptriggerdemo-test`.

**Step 3: Smoke-test the image locally**

```bash
docker run --rm -p 7071:80 httptriggerdemo-test
```

In another terminal:

```bash
curl http://localhost:7071/api/hello
```

Expected: `Hello, world!` with HTTP 200.

Stop the container with Ctrl-C.

**Step 4: Commit**

```bash
git add HttpTriggerDemo/Dockerfile
git commit -m "feat(samples): add Dockerfile for Azure Functions integration testing"
```

---

### Task 6: Add Testcontainers package

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `HttpTriggerDemo.Tests/HttpTriggerDemo.Tests.csproj`

**Step 1: Add version to Directory.Packages.props**

Add inside the test packages `<ItemGroup>`:

```xml
<PackageVersion Include="Testcontainers" Version="3.10.0" />
```

> Check https://www.nuget.org/packages/Testcontainers for the latest stable version and adjust accordingly.

**Step 2: Add reference to HttpTriggerDemo.Tests.csproj**

Add inside an `<ItemGroup>`:

```xml
<PackageReference Include="Testcontainers" />
```

**Step 3: Restore**

```bash
dotnet restore HttpTriggerDemo.Tests
```

Expected: no errors.

---

### Task 7: Add FunctionAppContainerFixture

**Files:**
- Create: `HttpTriggerDemo.Tests/FunctionAppContainerFixture.cs`

**Step 1: Create the fixture**

```csharp
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace HttpTriggerDemo.Tests;

// Shared across all middleware test classes — one container per test session.
public sealed class FunctionAppContainerFixture : IAsyncLifetime
{
    public const string Name = "FunctionAppContainer";

    private readonly IContainer _container;
    private Uri _baseAddress = null!;

    public FunctionAppContainerFixture()
    {
        var gitRoot = CommonDirectoryPath.GetGitDirectory().DirectoryPath;
        var dockerfileDir = Path.Combine(gitRoot, "HttpTriggerDemo");

        var image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(dockerfileDir)
            .WithDockerfile("Dockerfile")
            .Build();

        _container = new ContainerBuilder()
            .WithImage(image)
            .WithPortBinding(80, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPort(80).ForPath("/api/hello")))
            .Build();
    }

    public HttpClient CreateClient() =>
        new() { BaseAddress = _baseAddress };

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _baseAddress = new Uri($"http://localhost:{_container.GetMappedPublicPort(80)}");
    }

    public async Task DisposeAsync() =>
        await _container.DisposeAsync().AsTask();
}

[CollectionDefinition(FunctionAppContainerFixture.Name)]
public class FunctionAppFixtureCollection : ICollectionFixture<FunctionAppContainerFixture> { }
```

**Step 2: Build**

```bash
dotnet build HttpTriggerDemo.Tests
```

Expected: no errors.

---

### Task 8: Write and run CorrelationIdMiddlewareTests

**Files:**
- Create: `HttpTriggerDemo.Tests/CorrelationIdMiddlewareTests.cs`

**Step 1: Create the test file**

```csharp
namespace HttpTriggerDemo.Tests;

[Collection(FunctionAppContainerFixture.Name)]
public class CorrelationIdMiddlewareTests(FunctionAppContainerFixture fixture)
{
    private const string Header = "X-Correlation-Id";

    [Fact]
    public async Task CorrelationId_WhenMissing_IsGeneratedInResponse()
    {
        var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/hello");

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.Contains(Header),
            $"Expected {Header} header in response");
        var value = response.Headers.GetValues(Header).Single();
        Assert.True(Guid.TryParse(value, out _),
            $"Expected a GUID but got: {value}");
    }

    [Fact]
    public async Task CorrelationId_WhenProvided_IsEchoedBack()
    {
        var client = fixture.CreateClient();
        var id = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/hello");
        request.Headers.Add(Header, id);

        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var echoedId = response.Headers.GetValues(Header).Single();
        Assert.Equal(id, echoedId);
    }
}
```

**Step 2: Run the tests**

```bash
dotnet test HttpTriggerDemo.Tests --filter "FullyQualifiedName~CorrelationIdMiddlewareTests"
```

Expected: both tests pass. Container is built and started on first run (may take 1-2 minutes). Subsequent runs are faster due to Docker layer caching.

---

### Task 9: Write and run ExceptionHandlingMiddlewareTests

**Files:**
- Create: `HttpTriggerDemo.Tests/ExceptionHandlingMiddlewareTests.cs`

**Step 1: Create the test file**

```csharp
namespace HttpTriggerDemo.Tests;

[Collection(FunctionAppContainerFixture.Name)]
public class ExceptionHandlingMiddlewareTests(FunctionAppContainerFixture fixture)
{
    [Fact]
    public async Task UnhandledException_Returns500()
    {
        var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/error");

        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task UnhandledException_ResponseBodyIsEmpty()
    {
        var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/error");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Empty(body);
    }
}
```

**Step 2: Run all middleware tests**

```bash
dotnet test HttpTriggerDemo.Tests --filter "FullyQualifiedName~MiddlewareTests"
```

Expected: all 4 middleware tests pass.

**Step 3: Run full test suite**

```bash
dotnet test
```

Expected: all 10 tests pass (6 unit + 4 integration).

**Step 4: Commit**

```bash
git add HttpTriggerDemo.Tests/FunctionAppContainerFixture.cs \
        HttpTriggerDemo.Tests/CorrelationIdMiddlewareTests.cs \
        HttpTriggerDemo.Tests/ExceptionHandlingMiddlewareTests.cs \
        HttpTriggerDemo.Tests/HttpTriggerDemo.Tests.csproj \
        Directory.Packages.props
git commit -m "feat(samples): add Testcontainers integration tests for middleware"
```

---

## Troubleshooting

**Container fails to start / times out on wait strategy**
- Run `docker logs <container-id>` to see the Functions host output
- If the host complains about `AzureWebJobsStorage`, set it to the Azurite connection string and add `WithImage("mcr.microsoft.com/azure-storage/azurite")` as a network dependency

**Base image not found for .NET 10**
- Check available tags at `mcr.microsoft.com/azure-functions/dotnet-isolated`
- Fallback: use a self-contained publish with `mcr.microsoft.com/dotnet/runtime:10.0` and install the Functions host manually (not recommended for samples)

**`CommonDirectoryPath.GetGitDirectory()` returns wrong path**
- Check that `.git` exists at the repository root
- Fallback: compute from the test assembly location:
  ```csharp
  var testDir = Path.GetDirectoryName(typeof(FunctionAppContainerFixture).Assembly.Location)!;
  var gitRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", ".."));
  var dockerfileDir = Path.Combine(gitRoot, "HttpTriggerDemo");
  ```
