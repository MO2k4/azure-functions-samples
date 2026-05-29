# AspireDemo

Companion sample for **Getting Started with .NET Aspire for Azure Functions** (Series 3, Part 1).

Two projects are added here. `AspireDemo.AppHost` composes the two existing Function Apps from `ProjectOrganizationDemo` (`OrderProcessor.Http`, `OrderProcessor.Queue`) into a single locally-orchestrated app, replacing per-machine `local.settings.json` connection strings with a shared `AppHost.cs`. `AspireDemo.ServiceDefaults` is the small class library whose `AddServiceDefaults()` call each Functions worker invokes to route logs, traces, and metrics to the Aspire dashboard via OpenTelemetry.

```text
AspireDemo/
├── AspireDemo.slnx                # includes the two Functions projects + Core via relative path
├── README.md
├── AspireDemo.AppHost/
│   ├── AspireDemo.AppHost.csproj  # Sdk="Aspire.AppHost.Sdk/13.3.5", references both Functions csprojs
│   ├── AppHost.cs                 # shared host storage + two AddAzureFunctionsProject calls
│   ├── Properties/launchSettings.json
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── aspire.config.json
└── AspireDemo.ServiceDefaults/
    ├── AspireDemo.ServiceDefaults.csproj  # OpenTelemetry + resilience packages
    └── Extensions.cs                      # AddServiceDefaults() — referenced by both Functions projects
```

## What this sample demonstrates

1. **One command replaces five.** `dotnet run --project AspireDemo.AppHost` boots Azurite, both Function Apps, and the Aspire dashboard. No `func start`, no `npx azurite`, no per-terminal env files.
2. **Shared host storage across two Function Apps.** A single `AddAzureStorage("host-storage").RunAsEmulator()` is chained into both `AddAzureFunctionsProject<T>(...).WithHostStorage(hostStorage)` calls, so both apps point at the same Azurite container and existing `[QueueTrigger("orders", Connection = "AzureWebJobsStorage")]` triggers resolve without code changes.
3. **`local.settings.json.example` trimmed to `FUNCTIONS_WORKER_RUNTIME` only.** The Aspire integration doc requires removing the `AzureWebJobsStorage` line so the template default doesn't spin up a second Azurite that conflicts with Aspire's. Both Functions projects in `ProjectOrganizationDemo` have been trimmed.
4. **End-to-end trace across both apps.** `CreateOrderFunction` writes to the `orders` queue via a `[QueueOutput("orders")]` binding, which `ProcessOrderFunction` consumes with `[QueueTrigger("orders")]`. Both default to `AzureWebJobsStorage` (the shared host storage), so a single POST produces a producer to queue to consumer span tree in the dashboard's Traces view.
5. **Telemetry via `AddServiceDefaults()`.** Each Functions worker's `Program.cs` calls `builder.AddServiceDefaults()` (from `AspireDemo.ServiceDefaults`), which registers the OpenTelemetry exporter that targets the dashboard. The call is a no-op under a standalone `func start` (no OTLP endpoint configured), so the same code runs both with and without Aspire.

## Run

```bash
cd AspireDemo
dotnet run --project AspireDemo.AppHost
```

The console prints `Dashboard: https://localhost:<port>/login?t=<token>`. Open it to see all three resources (Azurite, orders-http, orders-queue) in one view, with structured logs, traces, and metrics.

## Pre-requisites

- .NET 10 SDK (`global.json` pins 10.0.201, rolls forward to latest patch).
- Docker Desktop (or an equivalent container runtime) for Aspire's Azurite container.
- Aspire templates: `dotnet new install Aspire.ProjectTemplates@13.3.5` (only needed if you want to regenerate AppHost projects locally; not required to run this sample).

## Article

[Getting Started with .NET Aspire for Azure Functions](https://dev.to/mo2k4/getting-started-with-net-aspire-for-azure-functions) (Series 3, Part 1, publishes 2026-05-29).
