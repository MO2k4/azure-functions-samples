# ApimOrdersDemo

Companion sample for **Introduction to Azure API Management** (Azure API Management: From Gateway to APIOps, Part 1).

An order API on Azure Functions (isolated worker, .NET 10, Flex Consumption) behind Azure API Management. APIM imports the API from a hand-written OpenAPI spec and only lets callers through with a key from the `orders-partners` product and an Entra ID token with the `Orders.ReadWrite` app role (Part 3). It calls the function with its managed identity and the function key; Easy Auth and the key check on the function reject everything else. Part 4 adds a second API version, a free product next to the partner one, and the developer portal.

```text
ApimOrdersDemo/
  azure.yaml             azd: one service, orders-api (host: function)
  src/OrdersApi/         HTTP-triggered functions matching the spec, in-memory store
  infra/main.bicep       parameters, required tags, Log Analytics + Application Insights, wires the two modules
  infra/entra.bicep      app registrations (Microsoft Graph Bicep): orders API, function backend, test client
  infra/function.bicep   Flex Consumption app, storage (identity-based), the function key APIM sends
  infra/apim.bicep       service + managed identity + developer portal, named values, backend, version set with two API versions, policies, App Insights logger, two products, partners group, subscription
  infra/policies/        the global policy (platform-owned), plus mirrors of the Part 2 to 4 policy files
  openapi/               mirrors of the two specs for links in Parts 1 to 4
  apiops/                APIOps (Part 5): the API surface as artifacts (specs, API/operation/product policies), extract filter, prod overrides
  requests.http          401 without a key, 200 with one, header vs query, v2, the portal's preflight
  scripts/verify.sh      Part 3 and 4 checks against a deployment: token, audience, direct calls, ip-filter, versions, CORS, products
```

## Deploy

The tested path is the Azure CLI one below (`az deployment group create` plus `func azure functionapp publish`); it was run end to end on 2026-10-02, and again with `apimSku=BasicV2` and the policies on 2026-10-12. `azd up` is wired up through `azure.yaml` and `main.parameters.json` but hasn't been run against this sample.

```bash
cd ApimOrdersDemo
azd env new apim-orders
azd env set APIM_PUBLISHER_EMAIL you@example.com
azd env set APIM_SKU BasicV2            # the default; Consumption only without the Part 2 policies, see below
azd up
```

Without azd (tested):

```bash
az group create -n rg-apim-orders -l westeurope
az deployment group create -g rg-apim-orders -f infra/main.bicep \
  -p environmentName=apim-orders publisherEmail=you@example.com
func azure functionapp publish <FUNCTION_APP_NAME> --dotnet-isolated   # run in src/OrdersApi
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

## Policies per scope

Companion to Part 2, **Policies: The Heart of APIM**. Each scope gets its own `policy.xml`. Since Part 5 only the global one deploys from `infra/policies/`; the others live in the APIOps artifact tree (`apiops/apim-artifacts/`), where both `apim.bicep` and `apiops publish` read them.

The artifact tree is authoritative. `infra/policies/api-orders.xml`, `operation-*.xml`, `product-*.xml` and `openapi/*.yaml` are mirrors at the old paths, kept so links in Parts 1 to 4 still resolve; nothing deploys from them, and `MIRRORS_ONLY=1 scripts/verify.sh` reports any that drift. The files per scope:

| File | Scope | What it does |
|---|---|---|
| `infra/policies/global.xml` | all APIs | `cors` for the developer portal origin (named value `portal-origin`, Part 4); sets `x-correlation-id` on the request and returns it on the response; removes the `X-Powered-By` response header; `<forward-request />` in `backend` |
| `products/orders-partners/policy.xml` | product `orders-partners` | `rate-limit`: 60 calls per 60 seconds per subscription |
| `products/orders-free/policy.xml` | product `orders-free` | `rate-limit`: 5 calls per 60 seconds, `quota`: 1,000 calls per week, both per subscription (Part 4) |
| `apis/orders/policy.xml`, `apis/orders-v2/policy.xml` | APIs `orders` and `orders-v2` (identical files) | `ip-filter` (forbids a documentation range; swap in the ranges you want to block, or `action="allow"` for an allow-list), `validate-azure-ad-token` (audience = the orders API app, caller must be the test client app, `roles` must contain `Orders.ReadWrite`), `authentication-managed-identity` toward the backend app, routes to the `orders-func` backend, removes the subscription key header, sends the subscription name as `x-partner`, and a `<choose>` that adds `rate-limit-by-key` (3 calls a minute per subscription) to `POST` only |
| `apis/orders-v2/operations/list-orders/policy.xml` | operation `list-orders` of API `orders-v2` | `rewrite-uri` to the function's `v2/orders` route; query parameters are copied |
| `apis/orders/operations/get-order/policy.xml` | operation `get-order` | response cache for `GET /orders/{orderId}`, 30 seconds, per subscription (keyed on `x-partner`; `vary-by-developer` would share one entry between subscriptions that have no owner) |

Every narrower file starts each section with `<base />`, which runs the broader scope's section at that spot. An operation policy without it skips the product rate limit, the token check and the backend route; through this sample the call then reaches the function without a managed identity token or function key and gets a 401.

**These policies need `apimSku=BasicV2` (the default) or higher.** `rate-limit-by-key` isn't available on Consumption: the deployment fails with `Policy is not allowed in 'Consumption' sku`. The cache statements save on Consumption, but that tier has no built-in cache; Learn points to an external Redis-compatible cache instead. To deploy on Consumption, remove the `<choose>` block from both API `policy.xml` files. Product policies only run for product-scoped subscriptions like `contoso-shop`: the built-in all-access subscription (the portal test console) skips the rate limit.

### Policy notes

- **Portal editors per scope.** Global: APIs > All APIs > Inbound processing > `</>`. Product: Products > the product > Policies. API: the API > Design > All operations > `</>`. Operation: the API > Design > the operation > `</>`. Each editor has **Calculate effective policy**; the management API returns the same with `effective=true` on a policy GET.
- **`increment-condition` moves the check.** With an `increment-condition` expression on `rate-limit-by-key` (for example to count only `200` responses), the gateway checks the counter at the end of `outbound`, after it knows the status code, so the 429 arrives one call later than you'd expect.
- **Find the address the gateway sees before writing an `ip-filter` allow-list.** A public "what is my IP" service can report a different egress address than the one that reaches the gateway. Echo it from a temporary outbound statement, then use that value:

  ```xml
  <set-header name="x-seen-ip" exists-action="override">
    <value>@(context.Request.IpAddress)</value>
  </set-header>
  ```

  A `forbid` with the echoed address returned 403 within about 20 seconds of the save; `allow` with the documentation range locked everyone out just as fast.
- **`Request-Context` can't be removed by policy.** The gateway (most likely its Application Insights logging) adds it after `outbound`; even the gateway's own 403 from `ip-filter` carries it. `Server` is dropped by the gateway without any policy.
- **The `context` object.** `context.Request` (`Method`, `Url`, `Headers`, `Body`, `IpAddress`, `MatchedParameters`), `context.Response` (`StatusCode`, `Headers`, `Body`), `context.Subscription`, `context.Product`, `context.User`, `context.Api`, `context.Operation`, `context.Variables` (from `set-variable`), `context.RequestId`, `context.Elapsed`, `context.Deployment.Region`, and `context.LastError` in `on-error`.

## Entra ID and managed identity

Companion to Part 3, **JWT Validation and Managed Identity**. `azd up` now also registers three Entra ID applications through the Microsoft Graph Bicep extension (`infra/entra.bicep`, `infra/bicepconfig.json`), so the account that deploys needs permission to register applications in the tenant.

| App registration | Used as |
|---|---|
| `orders-api-<token>` | audience callers request tokens for (`ORDERS_API_APP_ID`); defines the `Orders.ReadWrite` app role; v2 tokens, so `aud` is the client ID; `appRoleAssignmentRequired`, so Entra only issues tokens to clients with a role assignment |
| `orders-backend-<token>` | `resource` of `authentication-managed-identity` and the only audience Easy Auth accepts (`BACKEND_APP_ID`) |
| `orders-test-client-<token>` | stands in for a partner service; has the `Orders.ReadWrite` role (`TEST_CLIENT_APP_ID`); the only app ID in the policy's `client-application-ids` (named value `orders-test-client-app-id`) |

- APIM has a system-assigned identity. Its policy gets a token for the backend app and sends it as `Authorization: Bearer`, replacing the caller's token.
- The function app has Easy Auth (`authsettingsV2` in `infra/main.bicep`): Microsoft provider, audience = backend app, `allowedPrincipals` = APIM's identity, `Return401` for everything else.
- The function key stays as a second layer while you migrate: the functions are still `AuthorizationLevel.Function`, and the `orders-func` backend still sends `x-functions-key` from the secret named value `orders-function-key`. A call needs both the managed identity token and the key. Once nothing else calls the function with the key, switch the triggers to `Anonymous` and remove the key.
- Each new partner app needs an `Orders.ReadWrite` role assignment (`appRoleAssignmentRequired` blocks tokens otherwise) and its client ID in `client-application-ids`. Pinning the IDs in the policy also rejects apps that got a role some other way.

Graph Bicep can't create client secrets. Create one for the test client and get a token:

```bash
eval "$(azd env get-values)"
CLIENT_SECRET=$(az ad app credential reset --id "$TEST_CLIENT_APP_ID" --display-name requests-http --years 1 --query password -o tsv)
curl -s "https://login.microsoftonline.com/$AZURE_TENANT_ID/oauth2/v2.0/token" \
  -d grant_type=client_credentials -d client_id="$TEST_CLIENT_APP_ID" \
  --data-urlencode client_secret="$CLIENT_SECRET" -d scope="$ORDERS_API_APP_ID/.default" | jq -r .access_token
```

To check the whole chain, run `scripts/verify.sh` with that secret. It sends a valid call, calls without a token, with the wrong audience, with a tampered signature and without a subscription key to the gateway, then calls the function directly without a token, with another app's token and with the partner's token, and prints status code and body start for each next to the code Learn documents. It also swaps the API policy for `validate-jwt` and `ip-filter` variants and restores the committed `apiops/apim-artifacts/apis/orders/policy.xml` at the end, so don't run it against a shared instance. The script hasn't been run against a live deployment yet.

```bash
export CLIENT_SECRET
scripts/verify.sh | tee verify.log
```

`azd down` removes the Azure resources but not the app registrations; delete them with `az ad app delete --id <app id>`.

## Developer portal, versions and products

Companion to Part 4, **Developer Portal and API Versioning**. Not deployed yet: the Bicep and the function project build clean, and the checks below are a runbook.

**Versions.** `infra/apim.bicep` puts both APIs into the version set `orders` with the path scheme (`versioningScheme: 'Segment'`):

| API | `apiVersion` | URL | `list-orders` returns |
|---|---|---|---|
| `orders` (Original) | none | `/sales/orders` | an array of orders |
| `orders-v2` | `v2` | `/sales/v2/orders` | `{ "items": [...], "nextLink": "orders?skip=50" }` |

`orders` stays the Original so existing callers keep their URL. Learn describes that shape for the portal's "add a version" flow; it doesn't show the ARM form, so check after the first deploy that `/sales/orders` still answers (`verify.sh` case P1). If ARM insists on an identifier, the fallback is `apiVersion: 'v1'`, which moves every existing call to `/sales/v1/orders`. Both versions get the same API policy (token check, backend, `POST` limit); v2's `list-orders` has an operation policy that rewrites to the function's `v2/orders` route. The `get-order` cache and the Application Insights diagnostics are only on the Original.

**Products.** Both products contain both versions, because each version is its own API.

| Product | Approval | Subscriptions per user | Limits | Visible to |
|---|---|---|---|---|
| `orders-free` | auto-approved | 1 | 5 calls/minute, 1,000 calls/week | Developers (signed-in users) |
| `orders-partners` | required | unlimited | 60 calls/minute | group `partners` |

`contoso-shop` was created with `state: 'active'`, so switching `orders-partners` to `approvalRequired: true` doesn't touch it; only new requests wait for approval.

**Portal.** `developerPortalStatus: 'Enabled'` turns the managed portal on (v2 tiers create the instance with it off). It lives at `https://<APIM_NAME>.developer.azure-api.net`. Publishing it the first time is manual: Azure portal > the instance > Developer portal > Portal overview, open the portal as administrator, then **Publish**. The global policy allows the portal origin in `cors`, so the try-it console's preflight gets an answer. The console still can't call the API with a key alone, because both versions require an Entra token; that needs OAuth 2.0 user authorization configured for the console, which needs an app registration and isn't part of the sample.

**Approve a partner.** Add a developer to the `partners` group (Azure portal > Users, or let them sign up in the portal and add them afterwards), let them request **Orders for partners** in the portal, then find and approve the subscription:

```bash
APIM=https://management.azure.com/subscriptions/<azure-subscription-id>/resourceGroups/<rg>/providers/Microsoft.ApiManagement/service/<APIM_NAME>
az rest --method get --url "$APIM/subscriptions?api-version=2024-05-01&\$filter=properties/state eq 'submitted'" \
  --query "value[].{name:name, scope:properties.scope, owner:properties.ownerId}" -o table
az rest --method patch --url "$APIM/subscriptions/<name>?api-version=2024-05-01" --body '{"properties":{"state":"active"}}'
```

Who gets the approval email is the **Subscription requests (requiring approval)** notification (Azure portal > the instance > Notifications).

`verify.sh` cases P1 to P8 cover the Original URL, v2's envelope, an unknown version, the portal origin's preflight, the free product's rate limit, and a partner subscription before and after approval.

## APIOps: the API surface in Git

Companion to Part 5, **APIOps for Azure API Management**. Build-only: nothing has been extracted from or published to a live instance. The artifact tree was written by hand to match `infra/apim.bicep`; after the first live deploy, run an extract and commit whatever it changes (expect reformatted policy XML and an `operationInformation.json` per operation).

**Who owns what.** One owner per resource, or Bicep and the publisher silently overwrite each other.

| Owner | Resources |
|---|---|
| Bicep (`infra/`) | service, managed identity, named values and the `orders-function-key` secret, backend `orders-func`, global policy, logger and the `orders` API diagnostics, group `partners`, subscription `contoso-shop` |
| APIOps (`apiops/apim-artifacts/`) | version set `orders`, APIs `orders` and `orders-v2` with their specs, API and operation policies, products `orders-free` and `orders-partners` with their policies and API/group links |

`apim.bicep` reads the specs and policies from `apiops/apim-artifacts/`, so there is one deployable copy either way (the files at the old paths are mirrors, see "Policies per scope"). The `apiSurfaceOwner` parameter decides who deploys them: `bicep` (default, Parts 1 to 4) or `apiops`, where Bicep skips the API surface and `apiops publish` deploys it:

```bash
azd env set APIM_API_SURFACE_OWNER apiops
```

Switch an instance that was deployed in `bicep` mode. On a new environment in `apiops` mode, the Bicep deploy fails on the diagnostics and the `contoso-shop` subscription until the APIs and the partner product exist; deploy once in `bicep` mode first, or publish before the Bicep deploy.

**Layout.**

```text
apiops/
  configuration.extractor.yaml   the filter: the APIOps-owned names, [] for every Bicep-owned type
  configuration.prod.yaml        prod overrides (orders-partners: one subscription per partner)
  apim-artifacts/                apis/, versionSets/, products/ in the APIOps CLI layout
```

**Run it locally** (Node.js 22+, signed in with `az login`):

```bash
cd apiops
npx @azure-tools/apiops-cli@1.0.5 extract --resource-group <rg> --service-name <APIM_NAME> \
  --filter configuration.extractor.yaml --no-transitive --output apim-artifacts
npx @azure-tools/apiops-cli@1.0.5 publish --resource-group <rg> --service-name <APIM_NAME> \
  --source apim-artifacts --dry-run
```

- Every key missing from the filter means "everything of that type", which is why the Bicep-owned types are listed as `[]`. `--no-transitive` is still needed: without it the extract follows the policies to the `orders-func` backend and the named values.
- `--remove-stale` (used in the workflow) deletes local files for resources that no longer exist in the instance, inside the filter's scope.
- Never publish with `--delete-unmatched` here: it deletes everything in the instance that isn't in `apim-artifacts/`, which is the whole Bicep side.
- `scripts/verify.sh` with `APIOPS=1` runs a read-only extract into a temp folder, diffs it against the committed tree, and runs a dry-run publish.

**Workflows** (repository root, `.github/workflows/`):

- `apim-orders-apiops-extract.yml`: nightly against `dev` as a drift detector, or manually against `dev` or `prod`; opens a PR with the changes. No diff, no PR. While a drift PR is open, later runs update the same branch and PR (`apiops-extract/<env>`) instead of opening another one, and a run whose extract matches `main` again closes it.
- `apim-orders-apiops-publish.yml`: dry run against `dev` on every PR that touches `ApimOrdersDemo/apiops/`; on merge to `main` it publishes the merged commit's changes to `dev`, then to `prod` after an approval on the `prod` environment.

A PR opened with the default `GITHUB_TOKEN` doesn't start other workflows, so the extract PR gets no dry-run check until someone pushes to it. The next extract run rebuilds that branch from `main` and force-pushes it, so a commit pushed by hand doesn't survive the night. Use a GitHub App token in `create-pull-request` if you want the check to run on its own.

**Identity.** The workflows sign in with OIDC through a user-assigned managed identity, which needs no app registration (the tenant this sample was built in doesn't allow those). Per environment, in GitHub: a `dev` and a `prod` environment (`prod` with required reviewers) with the variables `AZURE_CLIENT_ID` (the identity's client ID), `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `APIM_RESOURCE_GROUP`, `APIM_SERVICE_NAME`. In Azure (not run for this sample):

```bash
az identity create -g <rg> -n id-apiops-orders
az identity federated-credential create -g <rg> --identity-name id-apiops-orders -n github-dev \
  --issuer https://token.actions.githubusercontent.com \
  --subject repo:<owner>/<repo>:environment:dev --audiences api://AzureADTokenExchange
az role assignment create --assignee-object-id <identity principalId> --assignee-principal-type ServicePrincipal \
  --role "API Management Service Contributor" --scope <APIM resource ID>
```

Repeat the federated credential for `environment:prod`. Learn recommends a read-only identity for extract and a separate one for publish; this sample uses one per environment to stay short.

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
  --specification-format OpenApi --specification-path apiops/apim-artifacts/apis/orders/specification.yaml \
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
| `Consumption` | $0 | first 1M/month free, then ~$3.50 per million | cold, see the article |
| `BasicV2` (default) | ~$150/month (one unit) | 10M included, then ~$3.00 per million | warm |

West Europe list prices, October 2026. Consumption can't be changed into a v2 tier in place; switching means a new instance. The Flex Consumption function app and the storage account cost cents at demo volume.

Tear down with `azd down` or `az group delete -n <rg>`.

## Run the function locally

```bash
cd src/OrdersApi
cp local.settings.json.example local.settings.json
func start
```

Locally neither the function key nor Easy Auth is checked, so `curl http://localhost:7071/api/orders` works without APIM.
