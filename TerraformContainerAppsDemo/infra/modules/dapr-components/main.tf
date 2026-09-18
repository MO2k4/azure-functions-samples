# This module earns its place because it hard-codes the component types and versions, refuses an
# empty scopes list and hides the azureClientId plumbing. A generic "any Dapr component" module
# would be worse than writing the resource directly.

# init_timeout is documented as an ISO 8601 string with the example "5s". Real ISO 8601 durations
# look like PT30S, and PT30S is not what the provider accepts. The Go-style form is correct.
resource "azurerm_container_app_environment_dapr_component" "state_cosmos" {
  name                         = "statestore"
  container_app_environment_id = var.container_app_environment_id
  component_type               = "state.azure.cosmosdb"
  version                      = "v1"
  init_timeout                 = "30s"
  ignore_errors                = false
  scopes                       = var.state_store_scopes

  metadata {
    name  = "url"
    value = var.cosmos_endpoint
  }

  metadata {
    name  = "database"
    value = var.cosmos_database
  }

  metadata {
    name  = "collection"
    value = var.cosmos_container
  }

  metadata {
    name  = "azureClientId"
    value = var.dapr_identity_client_id
  }

  metadata {
    name  = "actorStateStore"
    value = "true"
  }
}

# v2, not the v1 in the provider's own registry example. v1 strips the state key prefix as though
# keyPrefix were always none, and there is no migration path from v1 to v2.
resource "azurerm_container_app_environment_dapr_component" "state_blob" {
  count = var.blob_state_store == null ? 0 : 1

  name                         = "checkpoints"
  container_app_environment_id = var.container_app_environment_id
  component_type               = "state.azure.blobstorage"
  version                      = "v2"
  scopes                       = var.state_store_scopes

  metadata {
    name  = "accountName"
    value = var.blob_state_store.account_name
  }

  metadata {
    name  = "containerName"
    value = var.blob_state_store.container_name
  }

  metadata {
    name  = "azureClientId"
    value = var.dapr_identity_client_id
  }
}

resource "azurerm_container_app_environment_dapr_component" "pubsub_servicebus" {
  name                         = "orders-pubsub"
  container_app_environment_id = var.container_app_environment_id
  component_type               = "pubsub.azure.servicebus.topics"
  version                      = "v1"
  scopes                       = var.pubsub_scopes

  metadata {
    name  = "namespaceName"
    value = var.service_bus_namespace_fqdn
  }

  metadata {
    name  = "azureClientId"
    value = var.dapr_identity_client_id
  }

  metadata {
    name  = "consumerID"
    value = "{appID}"
  }

  metadata {
    name  = "maxActiveMessages"
    value = "1000"
  }
}
