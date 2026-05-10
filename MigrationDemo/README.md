# MigrationDemo

Companion sample for the W20 article *When Azure Functions Fight Back: Signs You've Outgrown Them*.

The same workload (a payment-settlement batch drained from a Storage Queue) packaged for three different Azure hosts. The shared library is identical across all three; the only thing that changes is the hosting wrapper. Read the diffs side by side and the migration question stops being abstract.

```text
MigrationDemo/
├── MigrationDemo.slnx
├── Settlement.Core/                # shared class library (net10.0)
│   ├── Models/                     # Payment, SettlementBatch, SettlementProgress, SettlementResponse
│   ├── Configuration/              # SettlementOptions
│   └── Services/                   # IPaymentSettler + PaymentSettler, ISettlementGateway + FakeSettlementGateway
├── Settlement.FunctionApp/         # variant 1: isolated worker, Storage Queue trigger
│   ├── SettlementFunction.cs
│   ├── Program.cs
│   └── host.json
├── Settlement.AppService/          # variant 2: ASP.NET Core minimal API + BackgroundService
│   ├── Program.cs
│   ├── Configuration/              # QueueOptions
│   └── Services/                   # SettlementWorker, SettlementWorkerStatus
└── Settlement.ContainerApp/        # variant 3: Worker SDK + Dockerfile, KEDA-scaled
    ├── Program.cs
    ├── Configuration/              # QueueOptions
    ├── SettlementWorker.cs
    └── Dockerfile
```

## What stays the same

`Settlement.Core` is consumed unchanged by every variant:

- The same `Payment` and `SettlementBatch` records.
- The same `IPaymentSettler.SettleAsync(batch, progress, ct)` contract.
- The same `PaymentSettler` implementation walking the batch one payment at a time, calling `ISettlementGateway`, honouring the cancellation token, and reporting `SettlementProgress`.
- The same `FakeSettlementGateway` (deterministic per-payment latency plus a configurable failure rate) so each variant runs end-to-end against Azurite without any external service.

The shared library has zero Functions / ASP.NET / Worker references. Anything host-specific lives in the host project.

## What changes between variants

| Concern              | Settlement.FunctionApp                                          | Settlement.AppService                                                          | Settlement.ContainerApp                                            |
| -------------------- | --------------------------------------------------------------- | ------------------------------------------------------------------------------ | ------------------------------------------------------------------ |
| Hosting model        | Functions isolated worker (Consumption / Flex / Premium)        | ASP.NET Core Web App on App Service (Linux) or App Service Plan                | Worker SDK packaged as a Linux container, KEDA queue scaler        |
| Trigger surface      | `[QueueTrigger]` attribute, runtime decides when to invoke      | `BackgroundService` polling loop in `SettlementWorker`                         | `BackgroundService` polling loop, no web host                      |
| Per-invocation cap   | 10 min (Consumption), 30 min default / unbounded (Flex/Premium) | None: process stays alive until you cancel it                                  | None: container runs until you scale to zero                       |
| Pays for idle        | No (Consumption / Flex)                                         | Yes — App Service Plan is always-on                                            | No — KEDA scales to zero when queue is empty                       |
| Adds web endpoints   | Not without bolting on HTTP triggers                            | Trivial: `app.MapGet("/status", ...)` next to the worker                       | Not by default (Worker SDK has no HTTP server)                     |
| Cold start sensitive | Yes (Consumption)                                               | No (Always On)                                                                 | Yes — first replica boot from zero pulls the image                 |
| Image artefact       | Function App ZIP                                                | Web App ZIP / GitHub Actions deploy                                            | OCI container image                                                |
| Best fit             | Bursty traffic, short tasks, no long batches                    | Steady traffic, long-running work, you also want HTTP endpoints in the same DU | Bursty *and* long-running, you want scale-to-zero plus no timeouts |

## When each variant is the right call

- **Stay on `Settlement.FunctionApp`** when individual batches finish well under the plan timeout (rule of thumb: longest batch under half the cap), traffic is bursty, and you do not need always-on infra. This is the cheapest variant for genuine spike-and-drain workloads.
- **Move to `Settlement.AppService`** when you have outgrown the per-invocation timeout, you already operate App Service for other apps, and you want to expose HTTP endpoints (status, health, manual triggers) alongside the worker without splitting the deployable.
- **Move to `Settlement.ContainerApp`** when you want both: no per-invocation cap *and* scale-to-zero billing. Container Apps with a KEDA queue scaler is the closest non-Functions equivalent to "pay only when work is happening." You give up the deployment ergonomics of `func azure functionapp publish` and pick up an OCI image build.

The article walks through the signals that push a workload from each row to the next.

## Build

From the repo root:

```bash
dotnet build MigrationDemo/MigrationDemo.slnx
```

All four projects target `net10.0` and share `Directory.Build.props` (`TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, central package management).

## Run each variant locally

All three read from the same `settlement-batches` Storage Queue, so start Azurite once and pick a host:

```bash
docker run -d --rm -p 10000:10000 -p 10001:10001 mcr.microsoft.com/azure-storage/azurite
```

Drop a batch on the queue (1,000 payments, ~50 ms each = ~50 s of work):

```bash
az storage message put \
  --queue-name settlement-batches \
  --connection-string "UseDevelopmentStorage=true" \
  --content "$(cat <<'JSON'
{
  "BatchId":"b-001",
  "CutoffUtc":"2026-05-15T00:00:00Z",
  "Payments":[{"PaymentId":"p-1","Amount":12.5,"Currency":"EUR"}]
}
JSON
)"
```

### Function App

```bash
cd MigrationDemo/Settlement.FunctionApp
cp local.settings.json.example local.settings.json
func start
```

### App Service

```bash
cd MigrationDemo/Settlement.AppService
dotnet run
# health: GET http://localhost:5000/health
# live progress: GET http://localhost:5000/status
```

### Container App (run as a worker locally first)

```bash
cd MigrationDemo/Settlement.ContainerApp
dotnet run
```

For a real Container App deploy, build the image and push:

```bash
cd MigrationDemo
docker build -f Settlement.ContainerApp/Dockerfile -t settlement-worker:dev .
```

Then deploy to Azure Container Apps with a KEDA `azure-queue` scaler bound to the same `settlement-batches` queue.

## Why this matters for the article

The migration question rarely turns on cost alone — it turns on whether the abstraction is fighting your design. With three working hosts in front of you, "what would migration cost?" stops being a thought exercise. You can read the diffs, see what stays in `Settlement.Core`, and price the move in lines of code rather than guesses.
