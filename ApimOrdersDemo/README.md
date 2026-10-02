# ApimOrdersDemo

Companion sample for **Introduction to Azure API Management** (Azure API Management: From Gateway to APIOps, Part 1).

An order API on Azure Functions (isolated worker, .NET 10, Flex Consumption) behind Azure API Management. APIM imports the API from a hand-written OpenAPI spec, sends the function key to the backend, and only lets callers through with a key from the `orders-partners` product.

```text
ApimOrdersDemo/
  azure.yaml             azd: one service, orders-api (host: function)
  openapi/orders.yaml    the contract APIM imports (3 operations)
  src/OrdersApi/         HTTP-triggered functions matching the spec, in-memory store
  infra/main.bicep       parameters, required tags, Log Analytics + Application Insights, wires the two modules
  infra/function.bicep   Flex Consumption app, storage (identity-based), the function key APIM uses
  infra/apim.bicep       service, named value, backend, API import + policy, App Insights logger, product, subscription
  requests.http          401 without a key, 200 with one, header vs query
```

## Deploy

The tested path is the Azure CLI one below (`az deployment group create` plus `func azure functionapp publish`); it was run end to end on 2026-10-02. `azd up` is wired up through `azure.yaml` and `main.parameters.json` but hasn't been run against this sample.

```bash
cd ApimOrdersDemo
azd env new apim-orders
azd env set APIM_PUBLISHER_EMAIL you@example.com
azd env set APIM_SKU Consumption        # or BasicV2
azd up
```

Without azd (tested):

```bash
az group create -n rg-apim-orders -l westeurope
az deployment group create -g rg-apim-orders -f infra/main.bicep \
  -p environmentName=apim-orders publisherEmail=you@example.com apimSku=Consumption
func azure functionapp publish <FUNCTION_APP_NAME>   # run in src/OrdersApi
```

`infra/main.bicep` tags every resource with `cost-center`, `owner`, `environment` and `project`, because the subscription the sample was built on denies untagged resources. Change the `tags` parameter to fit your own policy.

## Get a subscription key

```bash
az rest --method post \
  --url "https://management.azure.com/subscriptions/<azure-subscription-id>/resourceGroups/<rg>/providers/Microsoft.ApiManagement/service/<APIM_NAME>/subscriptions/contoso-shop/listSecrets?api-version=2024-05-01" \
  --query primaryKey -o tsv
```

Put it and `APIM_GATEWAY_URL` + `/sales` into `requests.http`.

## Rotate or revoke a key

Rotate in two steps so partners never hold a dead key: regenerate the secondary, hand it over, and once they've switched regenerate the primary.

```bash
SUB=https://management.azure.com/subscriptions/<azure-subscription-id>/resourceGroups/<rg>/providers/Microsoft.ApiManagement/service/<APIM_NAME>/subscriptions/contoso-shop
az rest --method post --url "$SUB/regenerateSecondaryKey?api-version=2024-05-01"
az rest --method post --url "$SUB/regeneratePrimaryKey?api-version=2024-05-01"
```

To cut a partner off, set the subscription's `state` to `cancelled` (`az rest --method patch --url "$SUB?api-version=2024-05-01" --body '{"properties":{"state":"cancelled"}}'`). In testing, the key got a 401 within five seconds. That only holds while no product with `subscriptionRequired: false` contains the API: an open product lets every call through, cancelled key or not.

## Rename the key header

If a partner's client already sends its key as `X-Api-Key`, rename what the API accepts instead of asking them to change. On the `ordersApi` resource in `infra/apim.bicep`:

```bicep
subscriptionKeyParameterNames: {
  header: 'X-Api-Key'
  query: 'api-key'
}
```

The CLI equivalent is `--subscription-key-header-name` / `--subscription-key-query-param-name` on `az apim api update`.

## Import without Bicep

For an APIM instance that already exists:

```bash
az apim api import -g <rg> -n <APIM_NAME> \
  --api-id orders --path sales \
  --specification-format OpenApi --specification-path openapi/orders.yaml \
  --service-url https://<FUNCTION_APP_NAME>.azurewebsites.net/api
```

- `OpenApi` is YAML; use `OpenApiJson` for a JSON spec, and `--specification-url` instead of `--specification-path` for a spec behind a URL. In Bicep the matching `format` values are `openapi`, `openapi+json` and `openapi-link`.
- The display name comes from the spec's `info.title` and must be unique on the instance. Importing the same file a second time under a new `--api-id` fails with "API with specified name 'Orders API' already exists" until you pass `--display-name`.
- An API imported this way is in no product, so the `contoso-shop` key gets 401 "invalid subscription key" on it until you add it to `orders-partners`.

Check what the import created:

```bash
az apim api operation list -g <rg> -n <APIM_NAME> --api-id orders \
  --query "[].{name:name, method:method, url:urlTemplate}" -o table
```

Expect `list-orders` (GET `/orders`), `get-order` (GET `/orders/{orderId}`) and `create-order` (POST `/orders`), named after the spec's `operationId`s.

## Where the time goes: Application Insights

The function app sends host telemetry to the `appi-orders-*` Application Insights resource, and API Management logs every call to the `orders` API to the same resource (logger `appinsights`, 100% sampling). Per call you get two rows from the gateway and one from the function:

- `requests` with `cloud_RoleName` = the APIM instance: the gateway's total time for the call
- `dependencies` from the same operation: the gateway's call to the function
- `requests` with `cloud_RoleName` = the function app: the function invocation

One row per call through the gateway, split by layer (run with `az monitor app-insights query --app <APPINSIGHTS_NAME> -g <rg> --analytics-query "..."`):

```kusto
requests
| where timestamp > ago(1h) and cloud_RoleName startswith "apim-orders"
| project timestamp, operation_Id, gateway_ms = duration
| join kind=leftouter (dependencies
    | where cloud_RoleName startswith "apim-orders"
    | project operation_Id, backend_ms = duration) on operation_Id
| join kind=leftouter (requests
    | where cloud_RoleName startswith "func-orders"
    | project operation_Id, function_ms = duration,
              host = tostring(customDimensions.HostInstanceId)) on operation_Id
| project timestamp, gateway_ms, backend_ms, function_ms, host,
          gateway_own_ms = gateway_ms - backend_ms,
          functions_platform_ms = backend_ms - function_ms
| order by timestamp asc
```

`gateway_own_ms` is what the gateway spent outside the backend call. `functions_platform_ms` is the backend call minus the function's own execution: front end, host start, worker start. Whatever the client measured beyond `gateway_ms` happened before the gateway's clock started (DNS, TLS, an instance being assigned). A new `host` value means a fresh Functions host.

Function host starts (a cold function shows a `Host started` trace just before the first invocation):

```kusto
traces
| where timestamp > ago(1h) and message startswith "Host started"
| project timestamp, message
```

Ingestion lags a few minutes behind the call, and the CLI only queries the last hour unless you add `--offset` (for example `--offset 24h`).

## Cost

| `apimSku` | Idle cost | Calls | First call after idle |
|---|---|---|---|
| `Consumption` (default) | $0 | first 1M/month free, then ~$3.50 per million | cold, see the article |
| `BasicV2` | ~$150/month (one unit) | 10M included, then ~$3.00 per million | warm |

West Europe list prices, October 2026. Consumption can't be changed into a v2 tier in place; switching means a new instance. The Flex Consumption function app and the storage account cost cents at demo volume.

Tear down with `azd down` or `az group delete -n <rg>`.

## Run the function locally

```bash
cd src/OrdersApi
cp local.settings.json.example local.settings.json
func start
```

Locally the function key isn't checked, so `curl http://localhost:7071/api/orders` works without APIM.
