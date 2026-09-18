data "azurerm_client_config" "current" {}

locals {
  # Role GUIDs, not names. A renamed role keeps its ID, and a name costs a role-definitions
  # lookup on every plan.
  role_ids = {
    acr_pull         = "7f951dda-4ed3-4680-a7ca-43fe172d538d"
    kv_secrets_user  = "4633458b-17de-408a-b874-0445c86b69e6"
    sb_data_sender   = "69a216fc-b8fb-44d8-bc22-1f3c2cd27a39"
    sb_data_receiver = "4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0"
  }

  role_definition_prefix = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/providers/Microsoft.Authorization/roleDefinitions"

  # Grants are scoped to the individual queue, never to the namespace.
  app_queue_pairs = {
    for pair in setproduct(tolist(var.app_ids), keys(var.service_bus_queue_ids)) :
    "${pair[0]}.${pair[1]}" => {
      app_id = pair[0]
      queue  = pair[1]
    }
  }
}

resource "azurerm_user_assigned_identity" "app" {
  for_each = var.app_ids

  name                = "id-${each.value}-${var.environment}"
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags
}

# principal_id is unknown until apply, so Terraform orders the identity ahead of every grant
# below without any help. The edge it cannot see is the one from the container app to this
# grant; see the depends_on on the container-apps module call in the root module.
resource "azurerm_role_assignment" "acr_pull" {
  for_each = var.app_ids

  scope              = var.container_registry_id
  role_definition_id = "${local.role_definition_prefix}/${local.role_ids.acr_pull}"
  principal_id       = azurerm_user_assigned_identity.app[each.value].principal_id

  # Correct for a managed identity and wrong for a user or a group: it suppresses the existence
  # check that fails with PrincipalNotFound while Entra is still replicating the new principal.
  principal_type                   = "ServicePrincipal"
  skip_service_principal_aad_check = true
}

resource "azurerm_role_assignment" "key_vault_secrets" {
  for_each = var.app_ids

  scope                            = var.key_vault_id
  role_definition_id               = "${local.role_definition_prefix}/${local.role_ids.kv_secrets_user}"
  principal_id                     = azurerm_user_assigned_identity.app[each.value].principal_id
  principal_type                   = "ServicePrincipal"
  skip_service_principal_aad_check = true
}

resource "azurerm_role_assignment" "service_bus_sender" {
  for_each = local.app_queue_pairs

  scope                            = var.service_bus_queue_ids[each.value.queue]
  role_definition_id               = "${local.role_definition_prefix}/${local.role_ids.sb_data_sender}"
  principal_id                     = azurerm_user_assigned_identity.app[each.value.app_id].principal_id
  principal_type                   = "ServicePrincipal"
  skip_service_principal_aad_check = true
}

resource "azurerm_role_assignment" "service_bus_receiver" {
  for_each = local.app_queue_pairs

  scope                            = var.service_bus_queue_ids[each.value.queue]
  role_definition_id               = "${local.role_definition_prefix}/${local.role_ids.sb_data_receiver}"
  principal_id                     = azurerm_user_assigned_identity.app[each.value.app_id].principal_id
  principal_type                   = "ServicePrincipal"
  skip_service_principal_aad_check = true
}

# Cosmos data-plane access is not azurerm_role_assignment. A Cosmos DB Built-in Data Contributor
# handed out through the control plane grants nothing at the data plane, and the apply is green.
# 0001 is Data Reader, 0002 is Data Contributor.
resource "azurerm_cosmosdb_sql_role_assignment" "data_contributor" {
  for_each = var.app_ids

  resource_group_name = var.cosmosdb_resource_group_name
  account_name        = var.cosmosdb_account_name
  role_definition_id  = "${var.cosmosdb_account_id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
  principal_id        = azurerm_user_assigned_identity.app[each.value].principal_id
  scope               = "${var.cosmosdb_account_id}/dbs/${var.cosmosdb_database_name}"
}
