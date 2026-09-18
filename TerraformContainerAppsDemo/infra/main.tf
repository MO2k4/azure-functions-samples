data "azurerm_client_config" "current" {}

# azurerm has no provider-level default_tags, so the policy-required four go in a locals map and
# get assigned on every resource. merge() where a resource needs extras.
locals {
  required_tags = {
    "cost-center" = var.cost_center
    "owner"       = var.owner
    "environment" = var.environment
    "project"     = var.project
  }

  name_suffix           = "${var.workload}-${var.environment}"
  service_bus_namespace = "sb-${local.name_suffix}"
  orders_app_id         = "orders-api"
}

resource "azurerm_resource_group" "this" {
  name     = "rg-${local.name_suffix}"
  location = var.location
  tags     = local.required_tags
}

resource "azurerm_log_analytics_workspace" "this" {
  name                = "log-${local.name_suffix}"
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name
  sku                 = "PerGB2018"
  retention_in_days   = var.log_retention_days
  tags                = local.required_tags
}

# Premium, because Private Link and the region-specific data endpoint need it.
resource "azurerm_container_registry" "this" {
  name                          = "acr${var.workload}${var.environment}"
  location                      = azurerm_resource_group.this.location
  resource_group_name           = azurerm_resource_group.this.name
  sku                           = "Premium"
  admin_enabled                 = false
  public_network_access_enabled = false
  data_endpoint_enabled         = true
  tags                          = local.required_tags
}

resource "azurerm_key_vault" "this" {
  name                          = "kv-${local.name_suffix}"
  location                      = azurerm_resource_group.this.location
  resource_group_name           = azurerm_resource_group.this.name
  tenant_id                     = data.azurerm_client_config.current.tenant_id
  sku_name                      = "standard"
  rbac_authorization_enabled    = true
  purge_protection_enabled      = true
  soft_delete_retention_days    = 7
  public_network_access_enabled = false
  tags                          = local.required_tags

  network_acls {
    bypass         = "AzureServices"
    default_action = "Deny"
  }
}

resource "azurerm_key_vault_secret" "orders_api_key" {
  name         = "orders-api-key"
  value        = var.orders_api_key
  key_vault_id = azurerm_key_vault.this.id
  tags         = local.required_tags
}

# local_authentication_enabled = false, not local_authentication_disabled = true. v5 renamed the
# argument and flipped its polarity, so a straight copy of the v4 boolean turns key auth back on.
resource "azurerm_cosmosdb_account" "this" {
  name                          = "cosmos-${local.name_suffix}"
  location                      = azurerm_resource_group.this.location
  resource_group_name           = azurerm_resource_group.this.name
  offer_type                    = "Standard"
  kind                          = "GlobalDocumentDB"
  public_network_access_enabled = false
  local_authentication_enabled  = false
  tags                          = local.required_tags

  consistency_policy {
    consistency_level = "Session"
  }

  geo_location {
    location          = azurerm_resource_group.this.location
    failover_priority = 0
  }
}

resource "azurerm_cosmosdb_sql_database" "orders" {
  name                = "orders"
  resource_group_name = azurerm_resource_group.this.name
  account_name        = azurerm_cosmosdb_account.this.name
}

resource "azurerm_cosmosdb_sql_container" "state" {
  name                  = "state"
  resource_group_name   = azurerm_resource_group.this.name
  account_name          = azurerm_cosmosdb_account.this.name
  database_name         = azurerm_cosmosdb_sql_database.orders.name
  partition_key_paths   = ["/partitionKey"]
  partition_key_version = 2
}

# Premium: Standard has no Private Link support, so the private endpoint in the networking
# module would fail at apply rather than at plan.
resource "azurerm_servicebus_namespace" "this" {
  name                          = local.service_bus_namespace
  location                      = azurerm_resource_group.this.location
  resource_group_name           = azurerm_resource_group.this.name
  sku                           = "Premium"
  capacity                      = 1
  public_network_access_enabled = false
  local_auth_enabled            = false
  tags                          = local.required_tags
}

resource "azurerm_servicebus_queue" "orders" {
  name         = "orders"
  namespace_id = azurerm_servicebus_namespace.this.id
}

resource "azurerm_servicebus_topic" "order_events" {
  name         = "order-events"
  namespace_id = azurerm_servicebus_namespace.this.id
}

module "networking" {
  source = "./modules/networking"

  workload            = var.workload
  environment         = var.environment
  location            = var.location
  resource_group_name = azurerm_resource_group.this.name
  address_space       = var.address_space

  # 7 newbits on a /16 gives a /23. /27 is the legal minimum for workload profiles, but the
  # subnet cannot be resized afterwards and a /27 caps at 9 Dedicated nodes.
  aca_subnet_newbits = 7

  cosmosdb_account_id     = azurerm_cosmosdb_account.this.id
  servicebus_namespace_id = azurerm_servicebus_namespace.this.id
  key_vault_id            = azurerm_key_vault.this.id
  container_registry_id   = azurerm_container_registry.this.id

  tags = local.required_tags
}

module "identity" {
  source = "./modules/identity"

  environment         = var.environment
  location            = var.location
  resource_group_name = azurerm_resource_group.this.name
  app_ids             = [local.orders_app_id]

  container_registry_id = azurerm_container_registry.this.id
  key_vault_id          = azurerm_key_vault.this.id

  service_bus_queue_ids = {
    orders       = azurerm_servicebus_queue.orders.id
    order-events = azurerm_servicebus_topic.order_events.id
  }

  cosmosdb_account_id          = azurerm_cosmosdb_account.this.id
  cosmosdb_account_name        = azurerm_cosmosdb_account.this.name
  cosmosdb_resource_group_name = azurerm_resource_group.this.name
  cosmosdb_database_name       = azurerm_cosmosdb_sql_database.orders.name

  tags = local.required_tags
}

# Two edges Terraform cannot infer, both expressed here because they cross a module boundary.
#
# module.identity: the app references the identity and the registry login server. It never
# references the AcrPull grant, so the graph is free to create the app first, the first revision
# fails its image pull, and the second apply succeeds. That is how this ships undiagnosed.
#
# module.networking: the environment references the subnet, not the NSG association. Associate
# the NSG first or the environment comes up against an unprotected subnet.
module "container_apps" {
  source = "./modules/container-apps"

  workload            = var.workload
  environment         = var.environment
  location            = var.location
  resource_group_name = azurerm_resource_group.this.name

  log_analytics_workspace_id = azurerm_log_analytics_workspace.this.id
  infrastructure_subnet_id   = module.networking.container_apps_subnet_id
  internal_only              = true

  container_registry_login_server = azurerm_container_registry.this.login_server
  app_identities = {
    (local.orders_app_id) = {
      id        = module.identity.identity_ids[local.orders_app_id]
      client_id = module.identity.client_ids[local.orders_app_id]
    }
  }

  dedicated_workload_profiles = var.dedicated_workload_profiles

  apps = {
    (local.orders_app_id) = {
      image        = "${azurerm_container_registry.this.login_server}/orders-api:${var.image_tag}"
      target_port  = 8080
      cpu          = 0.5
      memory       = "1Gi"
      min_replicas = var.min_replicas
      max_replicas = var.max_replicas

      key_vault_secrets = {
        "orders-api-key" = azurerm_key_vault_secret.orders_api_key.versionless_id
      }

      ingress = {
        external_enabled = false
      }

      service_bus_scale_rule = {
        queue_name    = azurerm_servicebus_queue.orders.name
        namespace     = azurerm_servicebus_namespace.this.name
        message_count = 20
      }
    }
  }

  tags = local.required_tags

  depends_on = [module.identity, module.networking]
}

module "dapr_components" {
  source = "./modules/dapr-components"

  container_app_environment_id = module.container_apps.environment_id
  dapr_identity_client_id      = module.identity.client_ids[local.orders_app_id]

  cosmos_endpoint  = azurerm_cosmosdb_account.this.endpoint
  cosmos_database  = azurerm_cosmosdb_sql_database.orders.name
  cosmos_container = azurerm_cosmosdb_sql_container.state.name

  service_bus_namespace_fqdn = "${azurerm_servicebus_namespace.this.name}.servicebus.windows.net"

  # Read off the container app, not off a local string. A scope holding the container app name
  # loads the component nowhere and the first state call 500s; an omitted scope loads it
  # everywhere, including apps with no business holding a connection to the orders database.
  # Both are green at apply time.
  state_store_scopes = [module.container_apps.dapr_app_ids[local.orders_app_id]]
  pubsub_scopes      = [module.container_apps.dapr_app_ids[local.orders_app_id]]
}

# A private endpoint gets its records from private_dns_zone_group. The environment is not a
# private endpoint, so its default_domain zone is hand-built: wildcard for the apps, apex for
# the environment itself, both pointing at the internal load balancer address.
resource "azurerm_private_dns_zone" "container_apps_environment" {
  name                = module.container_apps.environment_default_domain
  resource_group_name = azurerm_resource_group.this.name
  tags                = local.required_tags
}

resource "azurerm_private_dns_zone_virtual_network_link" "container_apps_environment" {
  name                 = "link-cae-${local.name_suffix}"
  private_dns_zone_id  = azurerm_private_dns_zone.container_apps_environment.id
  virtual_network_id   = module.networking.vnet_id
  registration_enabled = false
  resolution_policy    = "Default"
  tags                 = local.required_tags
}

resource "azurerm_private_dns_a_record" "container_apps_wildcard" {
  name                = "*"
  private_dns_zone_id = azurerm_private_dns_zone.container_apps_environment.id
  ttl                 = 300
  records             = [module.container_apps.environment_static_ip]
  tags                = local.required_tags
}

resource "azurerm_private_dns_a_record" "container_apps_apex" {
  name                = "@"
  private_dns_zone_id = azurerm_private_dns_zone.container_apps_environment.id
  ttl                 = 300
  records             = [module.container_apps.environment_static_ip]
  tags                = local.required_tags
}
