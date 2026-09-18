# TerraformContainerAppsDemo

Companion Terraform for **Infrastructure as Code: Terraform for Your New Stack** (From Functions to
Cloud-Native, part 5.7). Every HCL block in the article is lifted from this tree.

This is the W38 architecture expressed as Terraform: an internal Container Apps environment on
workload profiles, sitting in a delegated subnet, talking to Cosmos DB, Service Bus, Key Vault and a
Premium registry over private endpoints, with one Dapr-enabled app and its two components.

## Version baseline

| | |
|---|---|
| Terraform | 1.16.3 (`required_version = ">= 1.16.0"`) |
| `hashicorp/azurerm` | 5.6.0, pinned exactly in `infra/versions.tf` |

Everything below validates on that pair and nothing else has been tested:

```bash
cd infra
terraform init -backend=false -upgrade
terraform validate
terraform fmt -check -recursive
```

Use `-upgrade`. A `terraform init` without it resolves whatever azurerm build is already in the
local plugin cache, which can be a 4.x even with a `~> 5.6` constraint, and the config then fails
validation with errors that look like bugs in the config. `terraform version -json` reports what was
actually selected; check that before doubting the HCL.

Second caveat: `terraform validate` does not decode the `provider` block, so a bogus provider-level
argument passes validate and passes `plan` too when no resource needs a provider instance. A green
validate is not evidence that an argument exists. `terraform providers schema -json` is.

## Layout

```
infra/
  versions.tf              required_version, azurerm 5.6.0, backend "azurerm" {}, provider block
  variables.tf             typed and validated inputs
  main.tf                  resource group, Log Analytics, ACR, Key Vault, Cosmos, Service Bus,
                           the four module calls, and the environment's own private DNS zone
  outputs.tf
  envs/
    dev.backend.hcl        partial backend config, selected at init
    dev.tfvars
    prod.backend.hcl
    prod.tfvars
  modules/
    networking/            VNet, delegated /23 subnet, NSG, private DNS zones, private endpoints
    identity/              user-assigned identities, control-plane and data-plane grants
    container-apps/        environment with workload profiles, container apps
    dapr-components/       state store and pub/sub
workflows/                 GitHub Actions, deliberately not under .github/ (see workflows/README.md)
```

Module boundaries sit where the **lifetime** changes, not where the Azure service changes. The
network lives for years and exports IDs. The environment is near-permanent and the apps change
weekly. Dapr components attach to the environment. Identity crosses both.

## Prerequisites

To validate: Terraform 1.16.3 and nothing else. `subscription_id` is required for plan and apply but
not for validate, which is what makes a credential-free validate job possible in CI.

To actually deploy:

- An Azure subscription, with `Microsoft.App` and `Microsoft.OperationalInsights` registered. The
  provider block sets `resource_provider_registrations = "legacy"` because v5 defaults it to `none`,
  and a fresh subscription otherwise fails the first apply with what reads like a permissions error.
- A storage account and container for state, matching `infra/envs/<env>.backend.hcl`, with the
  deploying identity granted Storage Blob Data **Contributor** scoped to the container. Reader is
  not enough, because `terraform plan` takes the lock and taking the lock writes blob metadata.
- The `orders-api` image already pushed to the registry. Nothing here builds it.
- `TF_VAR_orders_api_key` in the environment. It has no default on purpose.

```bash
cd infra
terraform init -reconfigure -backend-config=envs/prod.backend.hcl
terraform plan -var-file=envs/prod.tfvars -out=tfplan
terraform apply tfplan
```

## The failure modes this demonstrates

**AcrPull is not an implicit dependency.** The container app references the identity and the
registry login server. It never references the role assignment, so Terraform's graph is free to
create the app first, the first revision fails its image pull, the apply errors, and a re-run
succeeds. It ships undiagnosed and comes back on the next green-field environment. The fix is the
`depends_on = [module.identity, module.networking]` on the `container_apps` module call in
`infra/main.tf`. Contrast it with the edge next door that Terraform gets right unaided:
`azurerm_user_assigned_identity.app.principal_id` is unknown until apply, so the identity is always
ordered before the grant. Attribute references create edges; semantic prerequisites do not.

Two related problems `depends_on` cannot fix. Entra replication lag can still return
`PrincipalNotFound` from a brand new principal, which is what
`skip_service_principal_aad_check = true` is for (correct for a managed identity, wrong for a user
or a group). And a green apply is not a working grant: managed identity token backends cache per
resource for around 24 hours, and Cosmos data-plane assignments propagate on their own schedule.

**Dapr `scopes` takes Dapr app ids, and both ways of getting it wrong are silent.** A scope holding
the container app name, or the Terraform resource name, loads the component nowhere and the first
state call 500s at runtime. An omitted `scopes` loads the component everywhere, including apps with
no business holding a connection to the orders database, and Cosmos enforces a metadata request rate
limit shared across the whole account that new connections eat into. Both apply cleanly. The
container-apps module exports `dapr_app_ids` read back off `azurerm_container_app.this[*].dapr[0].app_id`,
and the root module wires that into `state_store_scopes`, so the two strings cannot drift.

**Workload profiles are a one-way door.** An environment created without an initial
`workload_profile` cannot have one added later, and removing the last profile forces a recreation,
which takes every container app and every Dapr component in the environment with it. The module
always emits a `Consumption` profile for that reason, and both tfvars files carry the same profile
shape so that dev can actually rehearse a prod change. `infrastructure_subnet_id`,
`internal_load_balancer_enabled`, `zone_redundancy_enabled`, `infrastructure_resource_group_name`
and `dapr_application_insights_connection_string` force replacement on the same resource.

**The subnet you cannot resize.** `/27` is the legal minimum for a workload profiles environment and
the subnet must be delegated to `Microsoft.App/environments`. The `/21` figure in older azurerm docs
is stale; the `/23`-with-no-delegation rule belongs to the legacy Consumption-only environment,
where the requirement inverts rather than relaxes. This tree designs to `/23` because the size is
immutable, a `/27` caps at 9 Dedicated nodes, and a single-revision rollout doubles the requirement
for the duration of the deployment. Do not size from Microsoft's reserved-IP count: the same doc set
states it as 11, 12 and 14 in three places.

**Cosmos data-plane RBAC is not `azurerm_role_assignment`.** A control-plane Cosmos DB Built-in Data
Contributor grants nothing at the data plane and the apply is green. `identity/main.tf` uses
`azurerm_cosmosdb_sql_role_assignment` with a `sqlRoleDefinitions/...0002` path under the account and
a data-plane `scope`.

**NSG rules for workload profiles, not the Consumption set.** Inbound rules mean nothing on an
*external* environment, because traffic arrives via the managed resource group's public IP and never
enters your subnet, which is why `internal_only` defaults to true. Once a catch-all inbound deny
exists, an explicit intra-subnet allow is mandatory, because an NSG is evaluated on both NICs and a
low-priority deny overrides the default `AllowVnetInBound` at 65000. Service tags work in
`source_address_prefix` but not in `source_address_prefixes`, and that one fails at apply, not at
validate. Never deny `168.63.129.16`.

**Private DNS is two separate jobs.** `private_dns_zone_group` on an endpoint writes the records; it
does not link the zone to the VNet, and an unlinked zone resolves to the public IP with no DNS error
anywhere. ACR Premium needs a second, region-specific data zone or pulls fail mid-download. And the
Container Apps environment is not a private endpoint at all, so its `default_domain` zone is
hand-built in `main.tf` with wildcard and apex A records pointing at `static_ip_address`.

**Tags have no provider-level shortcut.** azurerm has no `default_tags`. The issue asking for it is
closed as completed and the pull request adding it is still open and unmerged; the schema is the
tie-breaker and it says absent. The four policy-required tags are a `locals` map assigned on every
resource, with `merge()` where a resource needs extras.

Two smaller ones worth knowing, both marked in the code: Dapr's `azureClientId` wants the identity's
**client** ID while a role assignment wants the **principal** ID, and Dapr's `namespaceName` is a
FQDN while KEDA's `namespace` two files over is the bare name. Four GUIDs and two strings that look
interchangeable and are not, none of which fail at plan time.

## CI/CD

See [workflows/README.md](./workflows/README.md). The files are not under `.github/workflows/` on
purpose, so this samples repository never starts deploying to Azure.

## Article

Part of the **From Functions to Cloud-Native** series on dev.to. The previous instalment,
[Azure Front Door, API Management and Container Apps: What Each Hop Trusts](https://dev.to/martin_oehlert/azure-front-door-api-management-and-container-apps-what-each-hop-trusts-38ik),
describes the same architecture in prose, including why the HTTP scale rule counts a rate rather
than concurrency.
