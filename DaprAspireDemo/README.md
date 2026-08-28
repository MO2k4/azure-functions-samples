# DaprAspireDemo

An Aspire AppHost orchestrating two Minimal API services that talk to each other through Dapr
service invocation and share a Dapr state store. Companion sample for ".NET Aspire: Orchestrating
Cloud-Native Apps" (Series 5, article 4).

Self-contained: nothing here depends on `DaprWebApiDemo/` or `AspireDemo/`.

## Layout

| Project | What it is |
| --- | --- |
| `DaprAspireDemo.AppHost` | The orchestrator. Declares the state store component, both services, and one Dapr sidecar per service. |
| `DaprAspireDemo.ServiceDefaults` | Shared OpenTelemetry, health checks, resilience and service discovery wiring (`AddServiceDefaults()` / `MapDefaultEndpoints()`). |
| `DaprAspireDemo.OrderService` | Dapr app ID `order-service`. Calls inventory through the sidecar, writes orders to the state store. |
| `DaprAspireDemo.InventoryService` | Dapr app ID `inventory-service`. The invocation target; no Dapr package reference at all. |
| `components/statestore.yaml` | The Redis state store the `order-service` sidecar loads. |

## Prerequisites

- .NET SDK 10
- Aspire CLI 13.5 or later (`aspire --version`)
- Dapr CLI initialised (`dapr init`), which leaves `dapr_placement`, `dapr_redis`, `dapr_scheduler`
  and `dapr_zipkin` running
- A container runtime the Dapr CLI can reach. On Colima that means exporting `DOCKER_HOST` before
  anything that shells out to `dapr`:
  `export DOCKER_HOST=unix:///Users/$USER/.colima/default/docker.sock`

## Run

```bash
cd DaprAspireDemo/DaprAspireDemo.AppHost
dotnet run
```

The dashboard URL printed on startup carries a one-time login token
(`https://localhost:17004/login?t=...`); open that exact URL, not the bare host.

Four resources come up: `order-service`, `order-service-dapr-cli`, `inventory-service`,
`inventory-service-dapr-cli`. Each `*-dapr-cli` resource is a `dapr run` process whose command line
is visible in the dashboard's Source column, which is the fastest way to see which component files
its sidecar actually loaded.

## Exercise it

```bash
# Fulfillable: 201, and the order lands in Redis under order-service||ORD-1001
curl -X POST http://localhost:5037/orders \
  -H "Content-Type: application/json" \
  -d '{"orderId":"ORD-1001","customerId":"CUST-42","lines":[{"sku":"AZ-KEYBOARD","quantity":2,"unitPrice":79.99}]}'

# Read it back through the state store
curl http://localhost:5037/orders/ORD-1001

# Short on stock: 409 with the shortfall, and no state write
curl -X POST http://localhost:5037/orders \
  -H "Content-Type: application/json" \
  -d '{"orderId":"ORD-1002","customerId":"CUST-42","lines":[{"sku":"AZ-DOCK","quantity":1,"unitPrice":199.00}]}'
```

The first call produces a seven-span trace across four resources in the dashboard:

```
POST /orders/                                      order-service
  HTTP POST 200                                    order-service
    HTTP POST                                      order-service-dapr-cli
      CallLocal/inventory-service/stock/check      inventory-service-dapr-cli
        POST /stock/check                          inventory-service
  DATA state /dapr.proto.runtime.v1.Dapr/SaveState order-service-dapr-cli
  HTTP POST 200 order-service-dapr-cli             order-service
```

## Things worth knowing

**The component reference replaces the sidecar's component folder.** A sidecar with no component
reference is started with `--components-path ~/.dapr/components` and sees every component on the
machine. Add one `WithReference(stateStore)` and the sidecar is started with `--resources-path`
instead, pointing at one folder. The two flags do not merge: `~/.dapr/components` becomes invisible
to that sidecar.

**Without `DaprComponentOptions.LocalPath`, the state store is in-memory.** `AddDaprStateStore("x")`
on its own writes a component with `spec.type: state.in-memory` into a temp folder. Everything
works, `curl` round-trips, and every write is gone the moment the sidecar restarts. `components/statestore.yaml`
is what makes it Redis; `curl http://localhost:<daprHttpPort>/v1.0/metadata` reports the type the
sidecar actually loaded.

**The reference goes on the sidecar, not on the project.**
`project.WithDaprSidecar().WithReference(component)` is `[Obsolete]` and fails a build with
`TreatWarningsAsErrors`. The package README for 13.0.0 still shows that form.

**`builder.AddDaprSidecar("name")` does not exist.** Sidecars attach to a resource.

**The `http` launch profile needs `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true`.** The template does not
put it in the profile it generates, so the AppHost throws
`OptionsValidationException` on startup. Sidecar traces reach the dashboard on both profiles once
it starts.
