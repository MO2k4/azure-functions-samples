# Monitoring Design: Application Insights and OpenTelemetry

**Date:** 2026-03-31
**Status:** Approved
**Article:** Part 9 - Monitoring and Troubleshooting: Application Insights Basics

## Overview

Add production monitoring to two existing sample projects, each demonstrating a different telemetry path from the article:

- **HttpTriggerDemo**: Classic Application Insights SDK
- **EventHubDemo**: OpenTelemetry with Azure Monitor Exporter

Both projects get `LoggerMessage` source generators and `BeginScope` correlation patterns.

## HttpTriggerDemo: Classic Application Insights SDK

### NuGet Packages

```xml
<!-- Do NOT upgrade to 3.x: breaks Functions worker. See github.com/Azure/azure-functions-dotnet-worker/issues/3322 -->
<PackageReference Include="Microsoft.ApplicationInsights.WorkerService" Version="2.22.0" />
<PackageReference Include="Microsoft.Azure.Functions.Worker.ApplicationInsights" />
```

### Program.cs

Add after existing service registrations:

```csharp
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();
```

Remove the default Warning-only logging filter:

```csharp
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
```

### OrderFunction.cs

Add `BeginScope` wrapping the function body:

```csharp
using var scope = logger.BeginScope(new Dictionary<string, object>
{
    ["OrderId"] = order.ProductId, // available from request
    ["ProductId"] = order.ProductId
});
```

Note: OrderId isn't known until after `CreateOrderAsync` returns. Scope will carry ProductId and Quantity from the request; OrderId gets logged in the completion message.

### OrderService.cs

Remove `OrderId`/`ProductId` from individual log calls (inherited from scope). Simplify messages:
- `"Validating order"` (before validation)
- `"Order saved"` or `"Order created: {OrderId}"` (after save, where OrderId is newly generated)

### New File: Logging/OrderLogs.cs

```csharp
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

### host.json

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

## EventHubDemo: OpenTelemetry with Azure Monitor Exporter

### NuGet Packages

```xml
<PackageReference Include="Microsoft.Azure.Functions.Worker.OpenTelemetry" />
<PackageReference Include="Azure.Monitor.OpenTelemetry.Exporter" />
```

### Program.cs

Add after existing service registrations:

```csharp
builder.Services
    .AddOpenTelemetry()
    .UseFunctionsWorkerDefaults()
    .UseAzureMonitorExporter();
```

### SensorReadingFunction.cs

Add batch-level `BeginScope` wrapping the foreach loop:

```csharp
using var batchScope = logger.BeginScope(new Dictionary<string, object>
{
    ["BatchSize"] = events.Length
});
```

Inside the loop, per-event `BeginScope` with `DeviceId`:

```csharp
using var eventScope = logger.BeginScope(new Dictionary<string, object>
{
    ["DeviceId"] = reading.DeviceId
});
```

### SensorProcessorService.cs

Remove `DeviceId` from individual log calls (inherited from scope). Simplify messages:
- `"Temperature out of range, skipping"` (with Temperature still as a parameter for the value)
- `"Humidity out of range, skipping"` (with Humidity still as a parameter)
- `"Reading saved"`

### New File: Logging/SensorLogs.cs

```csharp
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

### host.json

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

## README.md

Update the project table to include all four projects with a Monitoring column:

| Project | Article | Monitoring |
|---------|---------|------------|
| HttpTriggerDemo | Part 2 | Classic Application Insights SDK |
| TriggerDemo | Part 3 | -- |
| ConfigurationDemo | Part 6 | -- |
| EventHubDemo | -- | OpenTelemetry + Azure Monitor |

Add a short paragraph noting that HttpTriggerDemo and EventHubDemo demonstrate the two monitoring approaches from Part 9.

## Files Changed

### HttpTriggerDemo
- `HttpTriggerDemo.csproj` (add 2 packages)
- `Program.cs` (add SDK registration + filter removal)
- `OrderFunction.cs` (add BeginScope + use LoggerMessage)
- `OrderService.cs` (simplify logging, use LoggerMessage)
- `Logging/OrderLogs.cs` (new)
- `host.json` (log levels + sampling)

### EventHubDemo
- `EventHubDemo.csproj` (add 2 packages)
- `Program.cs` (add OTel registration)
- `SensorReadingFunction.cs` (add BeginScope + use LoggerMessage)
- `SensorProcessorService.cs` (simplify logging, use LoggerMessage)
- `Logging/SensorLogs.cs` (new)
- `host.json` (log levels + sampling)

### Root
- `README.md` (update project table)
