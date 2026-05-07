# ProjectOrganizationDemo

Companion sample for **Structuring Complex Function Apps: Project Organization**.

Two Function Apps share one class library to demonstrate the patterns the article walks through:

```text
ProjectOrganizationDemo/
├── ProjectOrganizationDemo.slnx
├── OrderProcessor.Core/         # shared class library (net10.0, abstractions only)
│   ├── Models/                  # Order, OrderStatus
│   ├── Stores/                  # IOrderStore + Sql/Cosmos keyed implementations
│   ├── Validators/              # OrderValidator
│   └── Services/                # AddOrderServices DI extension
├── OrderProcessor.Http/         # HTTP Function App
│   ├── Functions/               # CreateOrderFunction, GetOrderFunction
│   ├── Models/                  # CreateOrderRequest
│   ├── Infrastructure/          # middleware, cross-cutting concerns
│   ├── Program.cs
│   └── host.json
└── OrderProcessor.Queue/        # Queue Function App
    ├── Functions/               # ProcessOrderFunction
    ├── Models/                  # OrderMessage
    ├── Infrastructure/
    ├── Program.cs
    └── host.json
```

## What this sample demonstrates

1. **A pure shared library.** `OrderProcessor.Core` references only `Microsoft.Extensions.*` abstractions — no `Microsoft.Azure.Functions.Worker.*` packages. Non-Functions consumers (ASP.NET Core, console apps, tests) can use it without dragging in the Functions SDK.
2. **Two Function Apps, one library.** `OrderProcessor.Http` and `OrderProcessor.Queue` both consume `Core` via project reference and call the same `AddOrderServices()` extension in `Program.cs`.
3. **Keyed Services on a primary constructor.** Function classes take `[FromKeyedServices(OrderStoreKeys.Sql)] IOrderStore` directly on the primary constructor parameter — no field assignment ceremony, typed key constants instead of string literals.
4. **The community `Functions/Services/Models/Infrastructure` layout.** Microsoft's official samples group per trigger; this demo uses a layered structure that scales better past 10-15 functions in a single app.

## Build

```bash
cd ProjectOrganizationDemo
dotnet build
```

## Run locally

Each Function App needs its own terminal:

```bash
cd OrderProcessor.Http
cp local.settings.json.example local.settings.json
func start --port 7071
```

```bash
cd OrderProcessor.Queue
cp local.settings.json.example local.settings.json
func start --port 7072
```

The Queue app needs Azurite running for its `AzureWebJobsStorage` and queue trigger:

```bash
npx azurite --silent --location /tmp/azurite &
```
