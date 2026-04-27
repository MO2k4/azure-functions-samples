# Monitoring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add production monitoring to HttpTriggerDemo (Classic Application Insights SDK) and EventHubDemo (OpenTelemetry + Azure Monitor Exporter), with LoggerMessage source generators and BeginScope correlation patterns in both.

**Architecture:** Two parallel monitoring paths demonstrating the article's two approaches. HttpTriggerDemo gets the classic SDK path (most Azure Functions projects). EventHubDemo gets the OpenTelemetry path (multi-backend, standards-based). Both share the same host.json logging/sampling pattern and the same structured logging approach via `[LoggerMessage]` source generators.

**Tech Stack:** .NET 10, Azure Functions v4 (isolated worker), Microsoft.ApplicationInsights.WorkerService 2.22.0, Microsoft.Azure.Functions.Worker.ApplicationInsights, Microsoft.Azure.Functions.Worker.OpenTelemetry, Azure.Monitor.OpenTelemetry.Exporter, xUnit 2.9.3

**Design spec:** `docs/plans/2026-03-31-monitoring-design.md`

---

## File Structure

### HttpTriggerDemo (Classic Application Insights)

| File | Action | Responsibility |
|------|--------|----------------|
| `Directory.Packages.props` | Modify | Add 2 new package versions |
| `HttpTriggerDemo/HttpTriggerDemo.csproj` | Modify | Add 2 package references |
| `HttpTriggerDemo/Logging/OrderLogs.cs` | Create | LoggerMessage source generator definitions |
| `HttpTriggerDemo/Program.cs` | Modify | Register App Insights SDK + remove default filter |
| `HttpTriggerDemo/OrderFunction.cs` | Modify | Add BeginScope correlation |
| `HttpTriggerDemo/OrderService.cs` | Modify | Use LoggerMessage, simplify log calls |
| `HttpTriggerDemo/host.json` | Modify | Log levels + sampling config |

### EventHubDemo (OpenTelemetry)

| File | Action | Responsibility |
|------|--------|----------------|
| `Directory.Packages.props` | Modify | Add 2 new package versions (same edit as above) |
| `EventHubDemo/EventHubDemo.csproj` | Modify | Add 2 package references |
| `EventHubDemo/Logging/SensorLogs.cs` | Create | LoggerMessage source generator definitions |
| `EventHubDemo/Program.cs` | Modify | Register OpenTelemetry + Azure Monitor exporter |
| `EventHubDemo/SensorReadingFunction.cs` | Modify | Add batch + per-event BeginScope |
| `EventHubDemo/SensorProcessorService.cs` | Modify | Use LoggerMessage, simplify log calls |
| `EventHubDemo/host.json` | Modify | Log levels + sampling config |

### Root

| File | Action | Responsibility |
|------|--------|----------------|
| `README.md` | Modify | Add Monitoring column to project table |

---

## Task 1: Add NuGet Package Versions to Central Package Management

**Files:**
- Modify: `Directory.Packages.props`

- [ ] **Step 1: Add the four new package versions**

Open `Directory.Packages.props` and add a new `<ItemGroup>` after the existing EventHubDemo.Tests group (line 26). Insert before the closing `</ItemGroup>` of the last group or as a new group:

```xml
  <ItemGroup>
    <!-- Monitoring: HttpTriggerDemo (Classic App Insights) -->
    <!-- Do NOT upgrade WorkerService to 3.x: breaks Functions worker. See github.com/Azure/azure-functions-dotnet-worker/issues/3322 -->
    <PackageVersion Include="Microsoft.ApplicationInsights.WorkerService" Version="2.22.0" />
    <PackageVersion Include="Microsoft.Azure.Functions.Worker.ApplicationInsights" Version="2.0.0" />

    <!-- Monitoring: EventHubDemo (OpenTelemetry) -->
    <PackageVersion Include="Microsoft.Azure.Functions.Worker.OpenTelemetry" Version="1.0.0" />
    <PackageVersion Include="Azure.Monitor.OpenTelemetry.Exporter" Version="1.3.0" />
  </ItemGroup>
```

Place this new `<ItemGroup>` between the EventHubDemo.Tests group and the shared Functions packages group (i.e., between lines 26 and 27 of the current file).

- [ ] **Step 2: Verify the solution restores cleanly**

Run:
```bash
cd /Users/martino/Work/github/azure-functions-samples
dotnet restore
```
Expected: Restore succeeds with no errors. All four new packages resolve.

- [ ] **Step 3: Commit**

```bash
git add Directory.Packages.props
git commit -m "$(cat <<'EOF'
feat: add monitoring NuGet package versions to central package management

Application Insights WorkerService 2.22.0 (pinned, 3.x breaks worker),
Functions Worker ApplicationInsights, OpenTelemetry worker support, and
Azure Monitor OpenTelemetry Exporter.
EOF
)"
```

---

## Task 2: Add Package References to HttpTriggerDemo

**Files:**
- Modify: `HttpTriggerDemo/HttpTriggerDemo.csproj`

- [ ] **Step 1: Add the two App Insights package references**

In `HttpTriggerDemo/HttpTriggerDemo.csproj`, add these two lines inside the existing `<ItemGroup>` that contains the other `<PackageReference>` elements (after line 11, before the closing `</ItemGroup>`):

```xml
    <PackageReference Include="Microsoft.ApplicationInsights.WorkerService" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.ApplicationInsights" />
```

The full `<ItemGroup>` should now be:

```xml
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Timer" />
    <PackageReference Include="Azure.Data.Tables" />
    <PackageReference Include="Microsoft.ApplicationInsights.WorkerService" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.ApplicationInsights" />
  </ItemGroup>
```

- [ ] **Step 2: Verify the project builds**

Run:
```bash
cd /Users/martino/Work/github/azure-functions-samples
dotnet build HttpTriggerDemo/HttpTriggerDemo.csproj
```
Expected: Build succeeds with no errors.

- [ ] **Step 3: Commit**

```bash
git add HttpTriggerDemo/HttpTriggerDemo.csproj
git commit -m "$(cat <<'EOF'
feat(HttpTriggerDemo): add Application Insights SDK packages
EOF
)"
```

---

## Task 3: Add Package References to EventHubDemo

**Files:**
- Modify: `EventHubDemo/EventHubDemo.csproj`

- [ ] **Step 1: Add the two OpenTelemetry package references**

In `EventHubDemo/EventHubDemo.csproj`, add these two lines inside the existing `<ItemGroup>` (after line 11, before the closing `</ItemGroup>`):

```xml
    <PackageReference Include="Microsoft.Azure.Functions.Worker.OpenTelemetry" />
    <PackageReference Include="Azure.Monitor.OpenTelemetry.Exporter" />
```

The full `<ItemGroup>` should now be:

```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.Azure.Functions.Worker" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.EventHubs" />
    <PackageReference Include="Microsoft.Azure.Cosmos" />
    <PackageReference Include="Newtonsoft.Json" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.OpenTelemetry" />
    <PackageReference Include="Azure.Monitor.OpenTelemetry.Exporter" />
  </ItemGroup>
```

- [ ] **Step 2: Verify the project builds**

Run:
```bash
cd /Users/martino/Work/github/azure-functions-samples
dotnet build EventHubDemo/EventHubDemo.csproj
```
Expected: Build succeeds with no errors.

- [ ] **Step 3: Commit**

```bash
git add EventHubDemo/EventHubDemo.csproj
git commit -m "$(cat <<'EOF'
feat(EventHubDemo): add OpenTelemetry and Azure Monitor Exporter packages
EOF
)"
```

---

## Task 4: Create LoggerMessage Source Generators for HttpTriggerDemo

**Files:**
- Create: `HttpTriggerDemo/Logging/OrderLogs.cs`

- [ ] **Step 1: Create the Logging directory and OrderLogs.cs**

Create `HttpTriggerDemo/Logging/OrderLogs.cs`:

```csharp
using Microsoft.Extensions.Logging;

namespace HttpTriggerDemo.Logging;

public static partial class OrderLogs
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Order {OrderId} created for {ProductId}")]
    public static partial void OrderCreated(ILogger logger, string orderId, string productId);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning,
        Message = "Order validation failed: {Reason}")]
    public static partial void OrderValidationFailed(ILogger logger, string reason);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information,
        Message = "Order saved to repository")]
    public static partial void OrderSaved(ILogger logger);
}
```

- [ ] **Step 2: Verify it compiles**

Run:
```bash
cd /Users/martino/Work/github/azure-functions-samples
dotnet build HttpTriggerDemo/HttpTriggerDemo.csproj
```
Expected: Build succeeds. The source generator produces the partial method implementations.

- [ ] **Step 3: Commit**

```bash
git add HttpTriggerDemo/Logging/OrderLogs.cs
git commit -m "$(cat <<'EOF'
feat(HttpTriggerDemo): add LoggerMessage source generators for order logging
EOF
)"
```

---

## Task 5: Create LoggerMessage Source Generators for EventHubDemo

**Files:**
- Create: `EventHubDemo/Logging/SensorLogs.cs`

- [ ] **Step 1: Create the Logging directory and SensorLogs.cs**

Create `EventHubDemo/Logging/SensorLogs.cs`:

```csharp
using Microsoft.Extensions.Logging;

namespace EventHubDemo.Logging;

public static partial class SensorLogs
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Information,
        Message = "Processing batch of {BatchSize} events")]
    public static partial void BatchReceived(ILogger logger, int batchSize);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning,
        Message = "Received null or unparseable event, skipping")]
    public static partial void InvalidEventSkipped(ILogger logger);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning,
        Message = "Temperature {Temperature} out of range, skipping")]
    public static partial void TemperatureOutOfRange(ILogger logger, double temperature);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning,
        Message = "Humidity {Humidity} out of range, skipping")]
    public static partial void HumidityOutOfRange(ILogger logger, double humidity);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Information,
        Message = "Reading saved: {Temperature}C, {Humidity}%")]
    public static partial void ReadingSaved(ILogger logger, double temperature, double humidity);
}
```

- [ ] **Step 2: Verify it compiles**

Run:
```bash
cd /Users/martino/Work/github/azure-functions-samples
dotnet build EventHubDemo/EventHubDemo.csproj
```
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add EventHubDemo/Logging/SensorLogs.cs
git commit -m "$(cat <<'EOF'
feat(EventHubDemo): add LoggerMessage source generators for sensor logging
EOF
)"
```

---

## Task 6: Wire Up Application Insights in HttpTriggerDemo Program.cs

**Files:**
- Modify: `HttpTriggerDemo/Program.cs:1-22`

- [ ] **Step 1: Update Program.cs with App Insights registration and filter removal**

Replace the full contents of `HttpTriggerDemo/Program.cs` with:

```csharp
using HttpTriggerDemo;
using HttpTriggerDemo.Middleware;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.UseMiddleware<ExceptionHandlingMiddleware>(); // outermost — catches all
builder.UseMiddleware<CorrelationIdMiddleware>();     // innermost — per-request

builder.Services.AddScoped<IOrderRepository, InMemoryOrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// The SDK registers a default LoggerFilterRule that suppresses everything below Warning
// for the Application Insights provider. Remove it so Information-level logs flow through.
builder.Logging.Services.Configure<LoggerFilterOptions>(options =>
{
    LoggerFilterRule? defaultRule = options.Rules.FirstOrDefault(
        rule => rule.ProviderName ==
            "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
    if (defaultRule is not null)
    {
        options.Rules.Remove(defaultRule);
    }
});

builder.Build().Run();

// Expose the implicit Program type so integration test projects can reference it
// via WebApplicationFactory<Program>.
public partial class Program { }
```

- [ ] **Step 2: Verify the project builds**

Run:
```bash
cd /Users/martino/Work/github/azure-functions-samples
dotnet build HttpTriggerDemo/HttpTriggerDemo.csproj
```
Expected: Build succeeds with no errors.

- [ ] **Step 3: Commit**

```bash
git add HttpTriggerDemo/Program.cs
git commit -m "$(cat <<'EOF'
feat(HttpTriggerDemo): register Application Insights SDK and remove default log filter
EOF
)"
```

---

## Task 7: Wire Up OpenTelemetry in EventHubDemo Program.cs

**Files:**
- Modify: `EventHubDemo/Program.cs:1-26`

- [ ] **Step 1: Update Program.cs with OpenTelemetry registration**

Replace the full contents of `EventHubDemo/Program.cs` with:

```csharp
using EventHubDemo;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

var cosmosConnectionString = builder.Configuration["CosmosDbConnection"]
    ?? throw new InvalidOperationException("CosmosDbConnection is not configured");

builder.Services.AddSingleton(_ => new CosmosClient(
    cosmosConnectionString,
    new CosmosClientOptions
    {
        ConnectionMode = ConnectionMode.Gateway,
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        }
    }));

builder.Services.AddScoped<ICosmosRepository, CosmosSensorRepository>();
builder.Services.AddScoped<ISensorProcessor, SensorProcessorService>();

builder.Services
    .AddOpenTelemetry()
    .UseFunctionsWorkerDefaults()
    .UseAzureMonitorExporter();

builder.Build().Run();
```

- [ ] **Step 2: Verify the project builds**

Run:
```bash
cd /Users/martino/Work/github/azure-functions-samples
dotnet build EventHubDemo/EventHubDemo.csproj
```
Expected: Build succeeds with no errors.

- [ ] **Step 3: Commit**

```bash
git add EventHubDemo/Program.cs
git commit -m "$(cat <<'EOF'
feat(EventHubDemo): register OpenTelemetry with Azure Monitor Exporter
EOF
)"
```

---

## Task 8: Add BeginScope and LoggerMessage to OrderFunction.cs

**Files:**
- Modify: `HttpTriggerDemo/OrderFunction.cs:1-24`

- [ ] **Step 1: Update OrderFunction.cs with BeginScope correlation**

Replace the full contents of `HttpTriggerDemo/OrderFunction.cs` with:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace HttpTriggerDemo;

public record CreateOrderRequest(string ProductId, int Quantity);

public class OrderFunction(IOrderService orderService, ILogger<OrderFunction> logger)
{
    [Function("CreateOrder")]
    public async Task<IActionResult> CreateOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequest req,
        [FromBody] CreateOrderRequest order)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["ProductId"] = order.ProductId,
            ["Quantity"] = order.Quantity
        });

        var result = await orderService.CreateOrderAsync(order);

        if (!result.IsSuccess)
            return new BadRequestObjectResult(result.Error);

        return new CreatedResult($"/orders/{result.Order!.OrderId}", result.Order);
    }
}
```

Key changes:
- Added `ILogger<OrderFunction> logger` to the primary constructor
- Added `BeginScope` with `ProductId` and `Quantity` from the request (OrderId isn't available until after `CreateOrderAsync` returns)
- Added `using Microsoft.Extensions.Logging;`

- [ ] **Step 2: Verify the project builds**

Run:
```bash
cd /Users/martino/Work/github/azure-functions-samples
dotnet build HttpTriggerDemo/HttpTriggerDemo.csproj
```
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add HttpTriggerDemo/OrderFunction.cs
git commit -m "$(cat <<'EOF'
feat(HttpTriggerDemo): add BeginScope correlation to OrderFunction
EOF
)"
```

---

## Task 9: Update OrderService.cs to Use LoggerMessage

**Files:**
- Modify: `HttpTriggerDemo/OrderService.cs:23-41`

- [ ] **Step 1: Update OrderService to use LoggerMessage source generators**

Replace the `OrderService` class (lines 23-41) in `HttpTriggerDemo/OrderService.cs`. The full file becomes:

```csharp
using HttpTriggerDemo.Logging;
using Microsoft.Extensions.Logging;

namespace HttpTriggerDemo;

public record Order(string OrderId, string ProductId, int Quantity);

public record OrderResult(bool IsSuccess, Order? Order, string? Error)
{
    public static OrderResult Success(Order order) => new(true, order, null);
    public static OrderResult Failure(string error) => new(false, null, error);
}

public interface IOrderRepository
{
    Task SaveAsync(Order order);
}

public interface IOrderService
{
    Task<OrderResult> CreateOrderAsync(CreateOrderRequest request);
}

public class OrderService(ILogger<OrderService> logger, IOrderRepository repository) : IOrderService
{
    public async Task<OrderResult> CreateOrderAsync(CreateOrderRequest request)
    {
        if (request.Quantity <= 0)
        {
            OrderLogs.OrderValidationFailed(logger, "Quantity must be greater than zero");
            return OrderResult.Failure("Quantity must be greater than zero");
        }

        var order = new Order(
            OrderId: "ORD-" + Guid.NewGuid().ToString("N")[..8],
            ProductId: request.ProductId,
            Quantity: request.Quantity);

        await repository.SaveAsync(order);
        OrderLogs.OrderSaved(logger);

        OrderLogs.OrderCreated(logger, order.OrderId, order.ProductId);

        return OrderResult.Success(order);
    }
}

internal sealed class InMemoryOrderRepository : IOrderRepository
{
    public Task SaveAsync(Order order) => Task.CompletedTask;
}
```

Key changes:
- Added `using HttpTriggerDemo.Logging;`
- Replaced `logger.LogInformation(...)` with `OrderLogs.OrderCreated(...)`, `OrderLogs.OrderSaved(...)`, and `OrderLogs.OrderValidationFailed(...)`
- DeviceId/ProductId no longer repeated in log message parameters (inherited from BeginScope in OrderFunction)

- [ ] **Step 2: Verify the project builds**

Run:
```bash
cd /Users/martino/Work/github/azure-functions-samples
dotnet build HttpTriggerDemo/HttpTriggerDemo.csproj
```
Expected: Build succeeds.

- [ ] **Step 3: Run existing tests to verify no regressions**

Run:
```bash
cd /Users/martino/Work/github/azure-functions-samples
dotnet test HttpTriggerDemo.Tests/HttpTriggerDemo.Tests.csproj --filter "Category!=Container"
```
Expected: All non-container tests pass. The unit tests for OrderService use NSubstitute mocks for ILogger, so the switch to LoggerMessage static methods should be transparent (the generated code calls the same ILogger interface underneath).

If tests fail because they were asserting specific `logger.LogInformation` calls via NSubstitute `Received()`, update those assertions. LoggerMessage source generators call `logger.Log()` under the hood, not `LogInformation` directly. The fix for each such test is to change from:
```csharp
logger.Received().LogInformation(Arg.Any<string>(), Arg.Any<object[]>());
```
to verifying the logger received any `Log` call with the expected level:
```csharp
logger.ReceivedWithAnyArgs().Log(LogLevel.Information, default, Arg.Any<object>(), Arg.Any<Exception?>(), Arg.Any<Func<object, Exception?, string>>());
```
Or simply remove log-verification assertions (they test logging internals, not behavior).

- [ ] **Step 4: Commit**

```bash
git add HttpTriggerDemo/OrderService.cs
git commit -m "$(cat <<'EOF'
feat(HttpTriggerDemo): use LoggerMessage source generators in OrderService
EOF
)"
```

---

## Task 10: Add BeginScope and LoggerMessage to SensorReadingFunction.cs

**Files:**
- Modify: `EventHubDemo/SensorReadingFunction.cs:1-31`

- [ ] **Step 1: Update SensorReadingFunction.cs with BeginScope and LoggerMessage**

Replace the full contents of `EventHubDemo/SensorReadingFunction.cs` with:

```csharp
using Azure.Messaging.EventHubs;
using EventHubDemo.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EventHubDemo;

public class SensorReadingFunction(
    ILogger<SensorReadingFunction> logger,
    ISensorProcessor processor)
{
    [Function(nameof(SensorReadingFunction))]
    public async Task Run(
        [EventHubTrigger("sensor-readings", Connection = "EventHubConnection")]
        EventData[] events)
    {
        using var batchScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["BatchSize"] = events.Length
        });

        SensorLogs.BatchReceived(logger, events.Length);

        foreach (var eventData in events)
        {
            var reading = JsonSerializer.Deserialize<SensorReading>(eventData.Body.Span);
            if (reading is null)
            {
                SensorLogs.InvalidEventSkipped(logger);
                continue;
            }

            using var eventScope = logger.BeginScope(new Dictionary<string, object>
            {
                ["DeviceId"] = reading.DeviceId
            });

            await processor.ProcessAsync(reading);
        }
    }
}
```

Key changes:
- Added batch-level `BeginScope` with `BatchSize`
- Added per-event `BeginScope` with `DeviceId`
- Replaced inline `logger.LogInformation` / `logger.LogWarning` with `SensorLogs.*` calls
- Added `using EventHubDemo.Logging;`

- [ ] **Step 2: Verify the project builds**

Run:
```bash
cd /Users/martino/Work/github/azure-functions-samples
dotnet build EventHubDemo/EventHubDemo.csproj
```
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add EventHubDemo/SensorReadingFunction.cs
git commit -m "$(cat <<'EOF'
feat(EventHubDemo): add BeginScope correlation and LoggerMessage to SensorReadingFunction
EOF
)"
```

---

## Task 11: Update SensorProcessorService.cs to Use LoggerMessage

**Files:**
- Modify: `EventHubDemo/SensorProcessorService.cs:1-38`

- [ ] **Step 1: Update SensorProcessorService to use LoggerMessage source generators**

Replace the full contents of `EventHubDemo/SensorProcessorService.cs` with:

```csharp
using EventHubDemo.Logging;
using Microsoft.Extensions.Logging;

namespace EventHubDemo;

public class SensorProcessorService(
    ILogger<SensorProcessorService> logger,
    ICosmosRepository repository) : ISensorProcessor
{
    private const double MinTemperature = -50.0;
    private const double MaxTemperature = 150.0;
    private const double MinHumidity = 0.0;
    private const double MaxHumidity = 100.0;

    public async Task ProcessAsync(SensorReading reading)
    {
        if (reading.Temperature < MinTemperature || reading.Temperature > MaxTemperature)
        {
            SensorLogs.TemperatureOutOfRange(logger, reading.Temperature);
            return;
        }

        if (reading.Humidity < MinHumidity || reading.Humidity > MaxHumidity)
        {
            SensorLogs.HumidityOutOfRange(logger, reading.Humidity);
            return;
        }

        await repository.SaveAsync(reading);

        SensorLogs.ReadingSaved(logger, reading.Temperature, reading.Humidity);
    }
}
```

Key changes:
- Added `using EventHubDemo.Logging;`
- Replaced `logger.LogWarning(...)` with `SensorLogs.TemperatureOutOfRange(...)` and `SensorLogs.HumidityOutOfRange(...)`
- Replaced `logger.LogInformation(...)` with `SensorLogs.ReadingSaved(...)`
- Removed `DeviceId` from log parameters (inherited from BeginScope in SensorReadingFunction)

- [ ] **Step 2: Verify the project builds**

Run:
```bash
cd /Users/martino/Work/github/azure-functions-samples
dotnet build EventHubDemo/EventHubDemo.csproj
```
Expected: Build succeeds.

- [ ] **Step 3: Run existing tests to verify no regressions**

Run:
```bash
cd /Users/martino/Work/github/azure-functions-samples
dotnet test EventHubDemo.Tests/EventHubDemo.Tests.csproj --filter "Category!=Container&Category!=Pipeline"
```
Expected: All non-container, non-pipeline unit tests pass. Same caveat as Task 9 Step 3 regarding NSubstitute log assertions.

- [ ] **Step 4: Commit**

```bash
git add EventHubDemo/SensorProcessorService.cs
git commit -m "$(cat <<'EOF'
feat(EventHubDemo): use LoggerMessage source generators in SensorProcessorService
EOF
)"
```

---

## Task 12: Update host.json for Both Projects

**Files:**
- Modify: `HttpTriggerDemo/host.json`
- Modify: `EventHubDemo/host.json`

- [ ] **Step 1: Update HttpTriggerDemo/host.json**

Replace the full contents of `HttpTriggerDemo/host.json` with:

```json
{
  "version": "2.0",
  "logging": {
    "logLevel": {
      "default": "Warning",
      "Function": "Information",
      "Host.Results": "Information",
      "Host.Aggregator": "Trace"
    },
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "maxTelemetryItemsPerSecond": 20,
        "excludedTypes": "Request;Exception"
      }
    }
  }
}
```

Key changes from current:
- Added `logLevel` section: `default` at Warning suppresses noisy framework logs, `Function` at Information keeps your function logs, `Host.Results` at Information enables the invocation-summary table, `Host.Aggregator` at Trace enables aggregated metrics
- Added `maxTelemetryItemsPerSecond: 20` to sampling
- Changed `excludedTypes` from `"Request"` to `"Request;Exception"` (never sample away exceptions)

- [ ] **Step 2: Update EventHubDemo/host.json**

Replace the full contents of `EventHubDemo/host.json` with the identical config:

```json
{
  "version": "2.0",
  "logging": {
    "logLevel": {
      "default": "Warning",
      "Function": "Information",
      "Host.Results": "Information",
      "Host.Aggregator": "Trace"
    },
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "maxTelemetryItemsPerSecond": 20,
        "excludedTypes": "Request;Exception"
      }
    }
  }
}
```

- [ ] **Step 3: Verify both projects build**

Run:
```bash
cd /Users/martino/Work/github/azure-functions-samples
dotnet build HttpTriggerDemo/HttpTriggerDemo.csproj && dotnet build EventHubDemo/EventHubDemo.csproj
```
Expected: Both build successfully. host.json is a content file, not compiled, so this is a sanity check.

- [ ] **Step 4: Commit**

```bash
git add HttpTriggerDemo/host.json EventHubDemo/host.json
git commit -m "$(cat <<'EOF'
feat: configure host.json log levels and sampling for both monitored projects

Set default to Warning, Function to Information, exclude Request and
Exception types from adaptive sampling.
EOF
)"
```

---

## Task 13: Update README.md

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Update the project table to include all four projects with a Monitoring column**

In `README.md`, replace the existing `## Projects` table (lines 6-10) with:

```markdown
## Projects

| Project | Article | Monitoring |
|---------|---------|------------|
| [HttpTriggerDemo](./HttpTriggerDemo) | [Your First Azure Function: HTTP Triggers Step-by-Step](https://dev.to/martin_oehlert/your-first-azure-function-http-triggers-step-by-step-ib8) | Classic Application Insights SDK |
| [TriggerDemo](./TriggerDemo) | [Beyond HTTP: Timer, Queue, and Blob Triggers](https://dev.to/martin_oehlert/beyond-http-timer-queue-and-blob-triggers) | -- |
| [ConfigurationDemo](./ConfigurationDemo) | Configuration Done Right (Part 6) | -- |
| [EventHubDemo](./EventHubDemo) | Event Hub Processing (Part 4+) | OpenTelemetry + Azure Monitor |

HttpTriggerDemo and EventHubDemo demonstrate the two monitoring approaches from [Part 9: Monitoring and Troubleshooting](https://dev.to/martin_oehlert/monitoring-and-troubleshooting-application-insights-basics).
```

Note: The article URLs for ConfigurationDemo and EventHubDemo may need updating once those articles are published. Use placeholder text for now if exact URLs are not available.

- [ ] **Step 2: Commit**

```bash
git add README.md
git commit -m "$(cat <<'EOF'
docs: update README project table with monitoring column
EOF
)"
```

---

## Task 14: Full Build and Test Validation

**Files:** None (validation only)

- [ ] **Step 1: Build the entire solution**

Run:
```bash
cd /Users/martino/Work/github/azure-functions-samples
dotnet build
```
Expected: All projects build successfully with no errors or warnings related to the monitoring changes.

- [ ] **Step 2: Run all unit tests (excluding container/pipeline tests)**

Run:
```bash
cd /Users/martino/Work/github/azure-functions-samples
dotnet test --filter "Category!=Container&Category!=Pipeline"
```
Expected: All tests pass. If any tests fail due to the LoggerMessage refactoring (NSubstitute assertions on specific logger method calls), fix them as described in Task 9 Step 3.

- [ ] **Step 3: Verify no files were missed**

Run:
```bash
cd /Users/martino/Work/github/azure-functions-samples
git diff HEAD --stat
```
Expected: No uncommitted changes. All files from the design spec's "Files Changed" section have been committed.

Cross-check against the design spec checklist:
- [x] `Directory.Packages.props` (4 new packages)
- [x] `HttpTriggerDemo/HttpTriggerDemo.csproj` (2 package references)
- [x] `HttpTriggerDemo/Logging/OrderLogs.cs` (new)
- [x] `HttpTriggerDemo/Program.cs` (App Insights + filter removal)
- [x] `HttpTriggerDemo/OrderFunction.cs` (BeginScope)
- [x] `HttpTriggerDemo/OrderService.cs` (LoggerMessage)
- [x] `HttpTriggerDemo/host.json` (log levels + sampling)
- [x] `EventHubDemo/EventHubDemo.csproj` (2 package references)
- [x] `EventHubDemo/Logging/SensorLogs.cs` (new)
- [x] `EventHubDemo/Program.cs` (OpenTelemetry)
- [x] `EventHubDemo/SensorReadingFunction.cs` (BeginScope + LoggerMessage)
- [x] `EventHubDemo/SensorProcessorService.cs` (LoggerMessage)
- [x] `EventHubDemo/host.json` (log levels + sampling)
- [x] `README.md` (project table)
