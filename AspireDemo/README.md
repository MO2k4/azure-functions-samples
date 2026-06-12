# AspireDemo

Companion sample for the **.NET Aspire for Azure Functions** series:
- Part 1: *Getting Started with .NET Aspire for Azure Functions* (host storage + two Function Apps under one AppHost).
- Part 2: *Azure Services as Aspire Resources: Service Bus, Storage, and Redis* (adds Service Bus, Redis, and a separate application-storage resource, all declared in the same `AppHost.cs`).

`AspireDemo.AppHost` composes the Function Apps from `ProjectOrganizationDemo` (`OrderProcessor.Http`, `OrderProcessor.Queue`, and the Part 2 `OrderProcessor.ServiceBus`) into a single locally-orchestrated app, replacing per-machine `local.settings.json` connection strings with a shared `AppHost.cs`. `AspireDemo.ServiceDefaults` is the small class library whose `AddServiceDefaults()` call each Functions worker invokes to route logs, traces, and metrics to the Aspire dashboard via OpenTelemetry.

```text
AspireDemo/
├── AspireDemo.slnx                # includes the two Functions projects + Core via relative path
├── README.md
├── AspireDemo.AppHost/
│   ├── AspireDemo.AppHost.csproj  # Sdk="Aspire.AppHost.Sdk/13.3.5"; Functions + ServiceBus + Storage + Redis hosting refs
│   ├── AppHost.cs                 # host storage + app-storage + Service Bus + Redis + three AddAzureFunctionsProject calls
│   ├── Properties/launchSettings.json
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── aspire.config.json
└── AspireDemo.ServiceDefaults/
    ├── AspireDemo.ServiceDefaults.csproj  # OpenTelemetry + resilience packages
    └── Extensions.cs                      # AddServiceDefaults(), referenced by both Functions projects
```

## What this sample demonstrates

1. **One command replaces five.** `dotnet run --project AspireDemo.AppHost` boots Azurite, both Function Apps, and the Aspire dashboard. No `func start`, no `npx azurite`, no per-terminal env files.
2. **Shared host storage across two Function Apps.** A single `AddAzureStorage("host-storage").RunAsEmulator()` is chained into both `AddAzureFunctionsProject<T>(...).WithHostStorage(hostStorage)` calls, so both apps point at the same Azurite container and existing `[QueueTrigger("orders", Connection = "AzureWebJobsStorage")]` triggers resolve without code changes.
3. **`local.settings.json.example` trimmed to `FUNCTIONS_WORKER_RUNTIME` only.** The Aspire integration doc requires removing the `AzureWebJobsStorage` line so the template default doesn't spin up a second Azurite that conflicts with Aspire's. Both Functions projects in `ProjectOrganizationDemo` have been trimmed.
4. **End-to-end trace across both apps.** `CreateOrderFunction` writes to the `orders` queue via a `[QueueOutput("orders")]` binding, which `ProcessOrderFunction` consumes with `[QueueTrigger("orders")]`. Both default to `AzureWebJobsStorage` (the shared host storage), so a single POST produces a producer to queue to consumer span tree in the dashboard's Traces view.
5. **Telemetry via `AddServiceDefaults()`.** Each Functions worker's `Program.cs` calls `builder.AddServiceDefaults()` (from `AspireDemo.ServiceDefaults`), which registers the OpenTelemetry exporter that targets the dashboard. The call is a no-op under a standalone `func start` (no OTLP endpoint configured), so the same code runs both with and without Aspire.

## What Part 2 adds

The Part 2 changes are purely additive; the Part 1 flow above is untouched. `OrderProcessor.ServiceBus` is a third Function App whose single `ConfirmOrderFunction` exercises all three new resources at once.

1. **Service Bus as one declaration.** `AddAzureServiceBus("messaging").RunAsEmulator()` plus `AddServiceBusQueue("orders")` starts the Service Bus emulator container *and* its required SQL Server backing container, generates the SA password, accepts both EULAs, and pre-provisions the `orders` queue from the declaration (no hand-written `Config.json`). `WithReference(messaging, "messaging")` auto-wires `[ServiceBusTrigger("orders", Connection = "messaging")]`. On Apple Silicon both images are amd64-only and run under Rosetta.
2. **A separate application-storage resource.** `AddAzureStorage("app-storage").RunAsEmulator()` is a second Azurite resource, distinct from host-storage, so application data does not commingle with the runtime's bookkeeping. `appStorage.AddBlobs("receipts")` + `WithReference(receipts, "receipts")` auto-wires `[BlobOutput("receipts/{OrderId}.json", Connection = "receipts")]`; the function returns the confirmed order and Aspire writes it to a receipt blob.
3. **Redis via `IConnectionMultiplexer`.** `AddRedis("cache")` runs a local Redis container; `WithReference(cache)` serves `builder.AddRedisClient("cache")` in the worker, which registers `IConnectionMultiplexer`. `ConfirmOrderFunction` uses a `SET NX` key as a cheap idempotency gate against Service Bus at-least-once redelivery. This `IConnectionMultiplexer` path works on every Functions plan (the Redis *trigger* extension needs Premium/Dedicated). Note Aspire provisions Redis with TLS and a generated password; the client integration handles both automatically.
4. **One declaration, two environments.** Every `RunAsEmulator()` / `AddRedis` is local-only; in publish mode the same code provisions the real Azure resource (Service Bus namespace, containerized Redis on ACA). No per-environment config files.

## What Part 3 adds

Part 3 deploys this same AppHost to Azure. The only code change is the publish target:
`builder.AddAzureContainerAppEnvironment("aca-env")` (from `Aspire.Hosting.Azure.AppContainers`),
which declares the Azure Container Apps environment every project and container is published into.
It is inert locally, so the Part 1/2 inner loop above is unchanged. The line is required since
Aspire 9.4, which removed the implicit azd-owned ACA environment; without it a publish has no
compute target.

Generate the deployment Bicep without touching a subscription:

```bash
cd AspireDemo
aspire publish -o ./aspire-output   # writes main.bicep + one module folder per resource
```

The output is git-ignored (reproduce it any time). Each Functions app is generated as an Azure
Container App with `kind: functionapp` (so ACA derives KEDA scaling from the triggers), an
internal-only ingress by default (add `.WithExternalHttpEndpoints()` to expose an HTTP trigger),
`minReplicas: 1`, a user-assigned managed identity, and per-resource role assignments
(Service Bus Data Owner on `messaging`, Storage Data Contributor on both accounts).

## Run

```bash
cd AspireDemo
dotnet run --project AspireDemo.AppHost
```

The console prints `Dashboard: https://localhost:<port>/login?t=<token>`. Open it to see every resource in one view, with structured logs, traces, and metrics: two Azurite containers (`host-storage`, `app-storage`), `messaging` (Service Bus emulator) plus its `messaging-mssql` SQL backing container, `cache` (Redis), and the three Function Apps (`orders-http`, `orders-queue`, `orders-sb`).

## Pre-requisites

- .NET 10 SDK (`global.json` pins 10.0.201, rolls forward to latest patch).
- Docker Desktop (or an equivalent container runtime). Part 2 adds the Service Bus emulator and its SQL Server backing container; both are amd64-only, so on Apple Silicon enable Rosetta-based emulation in your container runtime.
- Aspire templates: `dotnet new install Aspire.ProjectTemplates@13.3.5` (only needed if you want to regenerate AppHost projects locally; not required to run this sample).

## Articles

- [Getting Started with .NET Aspire for Azure Functions](https://dev.to/martin_oehlert/getting-started-with-net-aspire-for-azure-functions-2g88) (Series 3, Part 1).
- [Azure Services as Aspire Resources: Service Bus, Storage, and Redis](https://dev.to/martin_oehlert/azure-services-as-aspire-resources-service-bus-storage-and-redis-2260) (Series 3, Part 2).
- [Deploying .NET Aspire Apps to Azure: AZD, ACA, and What Aspire Generates](https://dev.to/martin_oehlert/deploying-net-aspire-apps-to-azure-azd-aca-and-what-aspire-generates-4kk1) (Series 3, Part 3).
