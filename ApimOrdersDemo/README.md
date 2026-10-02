# ApimOrdersDemo

Companion sample for **Introduction to Azure API Management** (Azure API Management: From Gateway to APIOps, Part 1).

An order API on Azure Functions (isolated worker, .NET 10, Flex Consumption) behind Azure API Management. APIM imports the API from a hand-written OpenAPI spec, sends the function key to the backend, and only lets callers through with a key from the `orders-partners` product.

```text
ApimOrdersDemo/
  azure.yaml             azd: one service, orders-api (host: function)
  openapi/orders.yaml    the contract APIM imports (3 operations)
  src/OrdersApi/         HTTP-triggered functions matching the spec, in-memory store
  infra/main.bicep       parameters, required tags, wires the two modules
  infra/function.bicep   Flex Consumption app, storage (identity-based), the function key APIM uses
  infra/apim.bicep       service, named value, backend, API import + policy, product, subscription
  requests.http          401 without a key, 200 with one, header vs query
```

## Deploy

```bash
cd ApimOrdersDemo
azd env new apim-orders
azd env set APIM_PUBLISHER_EMAIL you@example.com
azd env set APIM_SKU Consumption        # or BasicV2
azd up
```

Without azd:

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

## Import without Bicep

For an APIM instance that already exists:

```bash
az apim api import -g <rg> -n <APIM_NAME> \
  --api-id orders --path sales \
  --specification-format OpenApi --specification-path openapi/orders.yaml \
  --service-url https://<FUNCTION_APP_NAME>.azurewebsites.net/api
```

`OpenApi` is YAML; use `OpenApiJson` for a JSON spec.

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
