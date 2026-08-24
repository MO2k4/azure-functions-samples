# DaprWebApiDemo

Companion sample for the W35 article *Building Your First Dapr + Web API Service* (Series 5, Part 3).

Part 2 (`DaprDemo`) put three building blocks in one binary to show what a sidecar does. This one builds the service properly: two separate ASP.NET Core Minimal API projects, one calling the other by Dapr app ID, with the parts a real service needs and a getting-started sample skips. Optimistic concurrency on the write that needs it, a typed failure path for the call that crosses a process boundary, an inbound token check, and source-generated JSON on both sides.

```text
DaprWebApiDemo/
├── OrderApi/                                   # app ID: orders-api
│   ├── OrderApi.csproj
│   ├── Program.cs                              # AddDaprClient, CreateInvokeHttpClient, JSON wiring
│   ├── OrderApiJsonContext.cs                  # source-generated JSON for everything on a wire
│   ├── Security/
│   │   └── DaprApiTokenFilter.cs               # IEndpointFilter checking the inbound dapr-api-token
│   ├── Inventory/
│   │   ├── Result.cs                           # Result<T>.Success / .Failure + InvocationError
│   │   └── InventoryClient.cs                  # the only code that knows inventory-api exists
│   └── Orders/
│       ├── Order.cs                            # stored model, state store name, partition rule
│       ├── OrderEndpoints.cs                   # the /orders group and its filter
│       ├── CreateOrder/CreateOrderEndpoint.cs  # POST   /orders
│       ├── GetOrder/GetOrderEndpoint.cs        # GET    /orders/{customerId}/{orderId}
│       ├── ConfirmOrder/ConfirmOrderEndpoint.cs# POST   /orders/{customerId}/{orderId}/confirm
│       └── CancelOrder/CancelOrderEndpoint.cs  # POST   /orders/{customerId}/{orderId}/cancel
├── InventoryApi/                               # app ID: inventory-api
│   ├── InventoryApi.csproj                     # no Dapr package reference, on purpose
│   ├── Program.cs
│   ├── InventoryApiJsonContext.cs
│   └── Stock/StockEndpoints.cs                 # POST /stock/check, GET /stock/{sku}
├── components/
│   ├── local/statestore.redis.yaml             # state.redis          -> orderstore
│   └── azure/statestore.cosmos.yaml            # state.azure.cosmosdb -> orderstore
├── dapr.yaml                                   # multi-app run file (orders-api + inventory-api)
└── README.md
```

## What it shows

| Topic | Where | The point |
|---|---|---|
| Service invocation, current API | `Program.cs`, `Inventory/InventoryClient.cs` | `DaprClient.CreateInvokeHttpClient("inventory-api")`, not the `[Obsolete]` `InvokeMethodAsync` |
| Invocation failures as values | `Inventory/Result.cs` | The SDK stopped wrapping failures; a `Result<T>` puts the branches back in one place |
| `ERR_DIRECT_INVOKE` | `InventoryClient.TranslateFailureAsync` | HTTP 500 from the sidecar meaning "no such app ID", handled apart from a real 500 |
| Optimistic concurrency | `Orders/ConfirmOrder` | `GetStateAndETagAsync` → mutate → `TrySaveStateAsync(etag)` → re-read → retry, bounded |
| Atomic multi-key writes | `Orders/CancelOrder` | `ExecuteStateTransactionAsync`, explicit `byte[]` values, and why one transaction pins the partition for every other slice |
| The Cosmos DB `partitionKey` override | `Orders/Order.cs` (`OrderPartition`) | One partition value on every state call, and the route segment that makes a partitioned read possible |
| Inbound app auth | `Security/DaprApiTokenFilter.cs` | `APP_API_TOKEN` on the way in, `DAPR_API_TOKEN` on the way out, one header name |
| Source-generated JSON | `OrderApiJsonContext.cs` | Wired into the Dapr client and into Minimal API, deliberately in two different ways |
| A Dapr-free callee | `InventoryApi/` | Being reachable by app ID costs the target zero lines of code |

### Endpoints

| Method | Route | App | Purpose |
|---|---|---|---|
| `POST` | `/orders` | orders-api | Checks stock through inventory-api, then saves the order |
| `GET` | `/orders/{customerId}/{orderId}` | orders-api | Reads the order back out of the state store |
| `POST` | `/orders/{customerId}/{orderId}/confirm` | orders-api | The ETag read-modify-write, with a bounded retry |
| `POST` | `/orders/{customerId}/{orderId}/cancel` | orders-api | Writes the cancelled order and its audit record in one state transaction |

Every route past the create carries `{customerId}` because the customer ID is the Cosmos DB partition key for order documents, and a partitioned read has to name the partition before it can fetch the document the value lives in. `POST /orders` takes it in the body and answers with a `Location` of `/orders/{customerId}/{orderId}`. The state key itself is still the bare order ID: `orders-api||ORD-1001` in Redis, an item with `id` `orders-api||ORD-1001` and `partitionKey` `CUST-42` in Cosmos DB.
| `POST` | `/stock/check` | inventory-api | Answers a whole order in one call; the invocation target |
| `GET` | `/stock/{sku}` | inventory-api | Single-SKU read, for poking at the service directly |

### The four things worth reading the code for

**The conflict is a `bool`, not an exception.** `TrySaveStateAsync` returns `false` when the ETag no longer matches; the raw HTTP API reports the same conflict as a 500-class body containing `ERR_STATE_SAVE` and "possible etag mismatch". The plain `SaveStateAsync` takes no ETag parameter at all, so optimistic concurrency is opt-in by choosing a different method, not by passing an extra argument.

**The re-read inside the loop is load-bearing.** Retrying the save with the ETag from the failed attempt can never succeed: that ETag is exactly the one the store has moved past. `ConfirmOrderEndpoint` reads at the top of every attempt. Hoisting that read out of the loop converts a retry into an infinite loop, and it is the single easiest thing to get wrong here.

**A `partitionKey` override binds every operation on the key, not the one that needed it.** For non-actor state the Cosmos DB component partitions on the item's own physical state key, `orders-api||ORD-1001`, which application code cannot reproduce without hardcoding its own app ID. So an explicit `partitionKey` can never match the default: it always moves the document. `CancelOrder` has no choice about passing one, because Cosmos DB refuses a transaction spanning partitions, and that single forced override decides where order documents live for the whole service. `OrderPartition` in `Orders/Order.cs` is the one place that says so, and all four slices go through it.

**Dapr no longer wraps invocation failures.** The handler behind `CreateInvokeHttpClient` rewrites the URI and passes the response through untouched: it never checks the status code and never throws. A target the sidecar cannot route to arrives as an ordinary HTTP 500 whose body carries `ERR_DIRECT_INVOKE`, and a target that failed on its own arrives as an ordinary non-2xx. The obsolete `InvokeMethodAsync` used to throw `InvocationException` with `.AppId`, `.MethodName` and `.Response` attached; losing that is the real cost of the migration, and `InventoryClient` is what replaces it.

## Prerequisites

- [.NET 10 SDK](https://dot.net/download)
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) plus `dapr init`

```bash
brew install dapr/tap/dapr-cli   # macOS; see the docs for Linux and Windows
dapr init
```

`dapr init` leaves the Redis container this sample's local state store points at running on `localhost:6379`, alongside Zipkin, the placement service and the scheduler.

## Run it locally

```bash
cd DaprWebApiDemo
dotnet build
dapr run -f .
```

`dapr run -f .` reads `dapr.yaml`, starts both projects with a `daprd` sidecar each, and points them at `components/local`. Stop the set with `dapr stop -f .`. Logs land in `DaprWebApiDemo/.dapr/logs/`.

Place an order that inventory can fulfil:

```bash
curl -s -X POST http://localhost:5100/orders \
  -H 'Content-Type: application/json' \
  -d '{
        "orderId": "ORD-1001",
        "customerId": "CUST-42",
        "lines": [
          { "sku": "AZ-KEYBOARD", "quantity": 2, "unitPrice": 89.00 },
          { "sku": "AZ-MOUSE",    "quantity": 1, "unitPrice": 39.50 }
        ]
      }'
```

Place one it cannot. `AZ-DOCK` is out of stock and `AZ-MONITOR` has three on hand, so this comes back `409` with a shortfall per line:

```bash
curl -s -X POST http://localhost:5100/orders \
  -H 'Content-Type: application/json' \
  -d '{
        "orderId": "ORD-1002",
        "customerId": "CUST-42",
        "lines": [
          { "sku": "AZ-MONITOR", "quantity": 9, "unitPrice": 249.00 },
          { "sku": "AZ-DOCK",    "quantity": 1, "unitPrice": 129.00 }
        ]
      }'
```

Read it back, then confirm it. The customer ID in the path is the partition, not decoration; asking for `CUST-99/ORD-1001` gets a `404` on either store:

```bash
curl -s http://localhost:5100/orders/CUST-42/ORD-1001
curl -s -X POST http://localhost:5100/orders/CUST-42/ORD-1001/confirm
```

Confirming twice is a no-op, which is what makes the endpoint safe to retry from the outside as well as from inside its own loop.

### Watch the ETag retry actually fire

The loop only does something interesting under contention, so create the contention:

```bash
for i in $(seq 1 20); do curl -s -o /dev/null -X POST http://localhost:5100/orders/CUST-42/ORD-1001/confirm & done; wait
```

Whichever of those requests read the order before the winner wrote it will log `Lost the ETag race on order ORD-1001, attempt 1 of 5` into `DaprWebApiDemo/.dapr/logs/orders-api_app_*.log`. They all still end in a `200`: the retry re-reads, finds the order already confirmed, and returns it. Raise the loop count in the request burst if the race is too quick to lose.

### Cancel an order, and watch two keys commit together

Cancelling writes the order and a separate audit record in one `ExecuteStateTransactionAsync` call. Place something to cancel first, because `POST /orders/{customerId}/{orderId}/cancel` refuses a confirmed order with a `409`:

```bash
curl -s -X POST http://localhost:5100/orders \
  -H 'Content-Type: application/json' \
  -d '{
        "orderId": "ORD-1004",
        "customerId": "CUST-42",
        "lines": [ { "sku": "AZ-MOUSE", "quantity": 1, "unitPrice": 39.50 } ]
      }'

curl -s -X POST http://localhost:5100/orders/CUST-42/ORD-1004/cancel \
  -H 'Content-Type: application/json' \
  -d '{ "reason": "Customer changed their mind" }'
```

The response carries both halves of the transaction. The second key is visible in Redis next to the first:

```bash
docker exec dapr_redis redis-cli KEYS 'orders-api||ORD-1004*'
```

Cancelling twice is a no-op that reads the existing audit record back, the same way confirming twice is. Three things in `CancelOrderEndpoint` are worth the read: `StateTransactionRequest` takes `byte[]`, not a value, so the JSON serialisation is written out by hand through `OrderApiJsonContext`; the operations are `Upsert` or `Delete` only, because there is no read inside a transaction; and every operation carries the same `OrderPartition.For(customerId)` metadata, which does nothing at all on Redis and is the difference between a working and a failing transaction on Cosmos DB.

### Prove the app ID is the whole address

Call the sidecar directly, without the SDK, exactly as `CreateInvokeHttpClient` does underneath:

```bash
curl -s -X POST http://localhost:3500/v1.0/invoke/inventory-api/method/stock/check \
  -H 'Content-Type: application/json' \
  -d '{"orderId":"ORD-1003","lines":[{"sku":"AZ-MOUSE","quantity":1}]}'
```

Then ask for an app ID that does not exist, which is what `InventoryClient` translates into a `503`:

```bash
curl -s http://localhost:3500/v1.0/invoke/warehouse-api/method/stock/check
# {"errorCode":"ERR_DIRECT_INVOKE","message":"failed to invoke, id: warehouse-api, err: ..."}
```

Stop `inventory-api` on its own (`dapr stop --app-id inventory-api`) and `POST /orders` answers `503` with a `ProblemDetails` body, rather than the `HttpRequestException` a bare `EnsureSuccessStatusCode()` would have thrown.

### The physical key is not the order ID

```bash
docker exec dapr_redis redis-cli KEYS '*'
# "orders-api||ORD-1001"
```

The sidecar prefixes state keys with the app ID. Change it with the component's `keyPrefix` metadata field.

### Turn the inbound token check on

`DaprApiTokenFilter` fails open when `APP_API_TOKEN` is unset, which is what makes every `curl` above work. Export the variable before `dapr run` so both the app and `daprd` inherit it:

```bash
export APP_API_TOKEN=local-dev-app-token
dapr run -f .

curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5100/orders/CUST-42/ORD-1001
# 401

curl -s -H "dapr-api-token: $APP_API_TOKEN" http://localhost:5100/orders/CUST-42/ORD-1001
# 200
```

Calls arriving through the sidecar carry the header already; only direct calls to the app port have to add it. `dapr.yaml` has the variable listed and commented out in its `common.env` block.

## Run the same code against Azure

Nothing in `OrderApi/Orders` changes. Point both apps in `dapr.yaml` at the other folder and re-run:

```yaml
    resourcesPaths: ./components/azure
```

`components/azure/statestore.cosmos.yaml` keeps the same `metadata.name`, is written for Microsoft Entra ID with a user-assigned managed identity, and carries the three Cosmos DB gotchas Part 2 established: the partition key must be named `/partitionKey` exactly, the field is `collection` and not `container`, and the data plane needs a native Cosmos role assignment (`00000000-0000-0000-0000-000000000002`) on top of the Azure RBAC one, using the identity's object ID.

One more that only shows up once you go past single-key writes: Cosmos DB requires every item in a transaction to share a partition, and for non-actor state the default partition key value is the item's own state key. So `ExecuteStateTransactionAsync` over two different keys can fail on partitioning alone, and the fix is an explicit `metadata: { "partitionKey": "..." }` on every operation in the set. Nothing in the .NET signature hints at it.

That override has a cost the docs do not spell out, and it is the reason this sample looks the way it does. The default partition value is the *physical* key, `orders-api||ORD-1001`, and the app ID prefix is added by the sidecar, so no explicit value an application can compute will ever match it. The override always relocates the document. Once a key has been written under one, every later read and write of that key needs the same value or Cosmos DB looks in the partition the state key implies and finds nothing.

`CancelOrder` cannot avoid the override, so the choice it makes is binding on the rest of the service. Applying it there and nowhere else produces the worst outcome available: the cancel path writes the order into the customer's partition while `CreateOrder`, `GetOrder` and `ConfirmOrder` keep using the order key's own, so Cosmos DB ends up holding two documents with the same order ID and `GET` keeps returning the pre-cancellation one. On Redis, which has a single keyspace and drops the metadata, none of that is visible. All four slices therefore go through `OrderPartition.For(customerId)`, and that is what puts `{customerId}` in the routes: a partitioned read has to name the partition before it can fetch the document holding the value. Committing to `partitionKey` for one aggregate is a decision about every operation on it, and about the API shape, not about one endpoint.

## Two things this sample deliberately does not do

**No `Microsoft.Extensions.Http.Resilience` on the invocation client.** Dapr already retries service invocation by default, and its built-in retries cannot be fully disabled: the reserved `DaprBuiltInServiceRetries` policy can be overridden but not lowered past the built-in floor, and even a named policy on an app target may not replace them entirely. Adding a Polly retry strategy on top of the `HttpClient` from `CreateInvokeHttpClient` stacks a third layer that knows nothing about the other two. Pick one owner for retry and backoff. If a hard per-call deadline is needed, a client-side *timeout* without a retry is the part worth keeping.

**No `QueryStateAsync`.** The state query API is still `v1.0-alpha1` in the URL itself, and has stayed alpha across four consecutive Dapr releases while Bulk PubSub and the Jobs API graduated around it. Which stores implement it is not a documented capability flag; it has to be read out of `components-contrib` (Cosmos DB and Redis do, Blob Storage does not), and PostgreSQL v2 dropped support that v1 had. For anything shaped like "find orders where status = X", go to the store's own query surface behind your own read path.

## Build

From the repo root:

```bash
dotnet build DaprWebApiDemo/OrderApi/OrderApi.csproj
dotnet build DaprWebApiDemo/InventoryApi/InventoryApi.csproj
```

Both target `net10.0` and inherit `Directory.Build.props` (`TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, central package management, lock files). Package versions live in `Directory.Packages.props`; the Dapr SDK is pinned at 1.18.5.
