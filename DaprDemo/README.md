# DaprDemo

Companion sample for the W34 article *Introduction to Dapr for Azure Developers* (Series 5, Part 2).

One order-processing service exercising three Dapr building blocks: **service invocation**, **state management**, and **publish/subscribe**. The whole point of the folder layout is the `components/` split: `components/local` runs the sample on Redis, `components/azure` runs the identical binary on Cosmos DB, Service Bus, and Key Vault. No C# changes between them.

```text
DaprDemo/
├── DaprDemo.csproj
├── Program.cs                              # DI wiring: AddDaprClient, CreateInvokeHttpClient, UseCloudEvents, MapSubscribeHandler
├── Orders/
│   └── OrderEndpoints.cs                   # the endpoints and the order model
├── components/
│   ├── local/
│   │   ├── statestore.redis.yaml           # state.redis          -> orderstore
│   │   └── pubsub.redis.yaml               # pubsub.redis         -> orderpubsub
│   └── azure/
│       ├── statestore.cosmos.yaml          # state.azure.cosmosdb -> orderstore
│       ├── pubsub.servicebus.yaml          # pubsub.azure.servicebus.topics -> orderpubsub
│       └── secretstore.keyvault.yaml       # secretstores.azure.keyvault    -> ordersecrets
├── dapr.yaml                               # multi-app run file (orders-api + shipping-api)
└── README.md
```

## What it shows

| Building block | Where | What the sidecar does for you |
|---|---|---|
| Service invocation | `PlaceOrderAsync` posts to `/shipments` on `http://shipping-api` | Resolves the app ID, mTLS, retries, load balancing, trace propagation |
| State management | `SaveStateAsync` / `GetStateAsync<T>` / `DeleteStateAsync` on `orderstore` | Talks to Redis or Cosmos DB; the app never sees either SDK |
| Pub/sub | `PublishEventAsync` to `orders.created`, plus the `.WithTopic(...)` subscriber | CloudEvent envelope, at-least-once delivery, subscription management |

### Endpoints

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/orders` | Saves the order, invokes shipping for a quote, publishes `orders.created` |
| `GET` | `/orders/{orderId}` | Reads the order back out of the state store |
| `DELETE` | `/orders/{orderId}` | Deletes the order from the state store |
| `POST` | `/shipments` | The service-invocation target (the "shipping service") |
| `POST` | `/events/order-created` | The pub/sub subscriber; flips the order to `Confirmed` |
| `GET` | `/dapr/subscribe` | Served by `MapSubscribeHandler()`; this is how the sidecar finds the subscriber |

### Two app IDs, one binary

`dapr.yaml` starts the same assembly twice, as `orders-api` on port 5080 and `shipping-api` on port 5081. That is enough to make service invocation real: `orders-api` addresses `shipping-api` by app ID and never learns a hostname or a port. In a production system these would be separate deployables; nothing in the code changes when you split them.

Both app IDs also load the pub/sub component, and `consumerID` defaults to the app ID, so each gets its own consumer group and each receives every `orders.created` message. Two *replicas* of one app ID would compete for messages instead.

## Prerequisites

- [.NET 10 SDK](https://dot.net/download)
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) plus `dapr init`

```bash
brew install dapr/tap/dapr-cli   # macOS; see the docs for Linux and Windows
dapr init
```

`dapr init` leaves four containers running: Redis (the local state store and message broker), Zipkin, the placement service, and the scheduler service. It also writes `~/.dapr/bin`, `~/.dapr/components`, and `~/.dapr/config.yaml`. This sample ignores `~/.dapr/components` and passes its own `resourcesPaths`.

## Run it locally

```bash
cd DaprDemo
dotnet build
dapr run -f .
```

`dapr run -f .` reads `dapr.yaml`, starts both apps with a `daprd` sidecar each, and points them at `components/local`. Stop the set with `dapr stop -f .`. Logs land in `DaprDemo/.dapr/logs/`.

Place an order (all three building blocks fire in one request):

```bash
curl -s -X POST http://localhost:5080/orders \
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

Read it back. The subscriber has already handled the event, so `status` is `Confirmed`:

```bash
curl -s http://localhost:5080/orders/ORD-1001
curl -s -X DELETE http://localhost:5080/orders/ORD-1001
```

You can also call the sidecar directly, without the SDK, which is what the SDK is doing underneath:

```bash
curl -s http://localhost:3500/v1.0/invoke/shipping-api/method/shipments \
  -H 'Content-Type: application/json' \
  -d '{"orderId":"ORD-1002","customerId":"CUST-42","itemCount":3}'
```

The physical key in Redis is **not** `ORD-1001`. Dapr prefixes state keys with the app ID:

```bash
docker exec dapr_redis redis-cli KEYS '*'
# "orders-api||ORD-1001"
```

Run a single app instead of the pair when you only want to poke at state and pub/sub:

```bash
dapr run --app-id orders-api --app-port 5080 --resources-path ./components/local \
  -- dotnet run -- --urls http://localhost:5080
```

(`--resources-path` replaced `--components-path`; both still take `-d`, but the old name is deprecated.)

## Run the same code against Azure

Nothing in `Orders/OrderEndpoints.cs` changes. Point both apps in `dapr.yaml` at the other folder and re-run:

```yaml
    resourcesPaths: ./components/azure
```

```bash
dapr run -f .
```

`components/azure` is written for Microsoft Entra ID authentication with a user-assigned managed identity, so there are no keys in the YAML. Fill in the placeholders and provision:

1. **Cosmos DB** (`statestore.cosmos.yaml`): the container's partition key **must** be named `/partitionKey`, case-sensitive. The data plane does not use standard Azure RBAC, so on top of the identity you need a native Cosmos role assignment:

   ```bash
   az cosmosdb sql role assignment create \
     --account-name "$ACCOUNT_NAME" --resource-group "$RESOURCE_GROUP" \
     --scope "/" --principal-id "$OBJECT_ID" \
     --role-definition-id "00000000-0000-0000-0000-000000000002"
   ```

   `$OBJECT_ID` is the identity's object ID, not the client ID in the YAML. This cannot be done in the portal.

2. **Service Bus** (`pubsub.servicebus.yaml`): pre-create the `orders.created` topic and one subscription per app ID (`orders-api`, `shipping-api`), because the component sets `disableEntityManagement: "true"`. Then assign scoped Data Sender / Data Receiver. Leave entity management on instead and the sidecar creates topics and subscriptions itself, which is an admin operation and needs Azure Service Bus Data Owner.

3. **Key Vault** (`secretstore.keyvault.yaml`): create the vault with `--enable-rbac-authorization true` and assign **Key Vault Secrets User**. Store the Service Bus connection string as `orders-servicebus-connection`. Key Vault is a name/value store, so `secretKeyRef.name` and `secretKeyRef.key` must be identical.

### Where the abstraction leaks

Swapping the YAML is real; behavioural equivalence is not. Cosmos DB supports CRUD, transactions, ETags, TTL, and actors, but not the workflow building block. Move `orderstore` to Blob Storage or Table Storage and the code still compiles while transactions, TTL, and actors quietly stop working. Read the [supported state stores matrix](https://docs.dapr.io/reference/components-reference/supported-state-stores/) before you swap a `spec.type`.

### Azure Container Apps

The component files here use the open-source schema. Container Apps uses a simplified one: `componentType` replaces `spec.type`, there is no `spec` wrapper, and the component name is not in the YAML at all (it comes from `--dapr-component-name`). Components are environment-level resources:

```bash
az containerapp env dapr-component set \
  --name cae-orders --resource-group rg-orders \
  --dapr-component-name orderstore --yaml ./statestore.cosmos.yaml
```

`scopes` there matches the Dapr **app ID**, not the container app name. And the Dapr `Configuration` spec is not exposed at all, which means `secrets.scopes` is unavailable: every secret in the vault stays readable by every scoped app. Give the app its own vault.

## Notes on the .NET SDK

`DaprClient.InvokeMethodAsync`, `InvokeMethodWithResponseAsync`, and `InvokeMethodGrpcAsync` have all carried `[Obsolete]` since Dapr .NET SDK **1.17** ("Recommended guidance is to use a native HTTP or gRPC client for service invocation"), and the docs still teach them. `Program.cs` uses `DaprClient.CreateInvokeHttpClient` instead, which is the supported path and is not deprecated.

The consequence is worth stating plainly: Dapr does not replace your `HttpClient`. You keep the `HttpClient`; Dapr replaces the service discovery, mTLS, retry policy, and tracing behind it.

The state and pub/sub methods (`SaveStateAsync`, `GetStateAsync<T>`, `DeleteStateAsync`, `PublishEventAsync`) are not deprecated.

## Build

From the repo root:

```bash
dotnet build DaprDemo/DaprDemo.csproj
```

Targets `net10.0` and inherits `Directory.Build.props` (`TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, central package management, lock files). Package versions live in `Directory.Packages.props`.
