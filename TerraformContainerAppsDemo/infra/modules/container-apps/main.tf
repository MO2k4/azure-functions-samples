locals {
  missing_identities = setsubtract(keys(var.apps), keys(var.app_identities))
}

# Every interesting argument here is a one-way door. infrastructure_subnet_id,
# internal_load_balancer_enabled, zone_redundancy_enabled, infrastructure_resource_group_name
# and dapr_application_insights_connection_string all force replacement, and so does adding the
# first workload_profile or removing the last one. A replacement takes every app and every Dapr
# component in the environment with it.
resource "azurerm_container_app_environment" "this" {
  name                = "cae-${var.workload}-${var.environment}"
  location            = var.location
  resource_group_name = var.resource_group_name

  logs_destination           = "log-analytics"
  log_analytics_workspace_id = var.log_analytics_workspace_id

  infrastructure_subnet_id           = var.infrastructure_subnet_id
  infrastructure_resource_group_name = "rg-${var.workload}-${var.environment}-aca-infra"
  internal_load_balancer_enabled     = var.internal_only
  zone_redundancy_enabled            = var.environment == "prod"

  dapr_application_insights_connection_string = var.dapr_application_insights_connection_string

  workload_profile {
    name                  = "Consumption"
    workload_profile_type = "Consumption"
  }

  dynamic "workload_profile" {
    for_each = var.dedicated_workload_profiles

    content {
      name                  = workload_profile.key
      workload_profile_type = workload_profile.value.profile_type
      minimum_count         = workload_profile.value.minimum_count
      maximum_count         = workload_profile.value.maximum_count
    }
  }

  tags = var.tags

  lifecycle {
    precondition {
      condition     = length(local.missing_identities) == 0
      error_message = "Every app needs an identity: ${join(", ", local.missing_identities)} are in apps but not in app_identities."
    }
  }
}

resource "azurerm_container_app" "this" {
  for_each = var.apps

  name                         = "ca-${each.key}-${var.environment}"
  container_app_environment_id = azurerm_container_app_environment.this.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"
  workload_profile_name        = each.value.workload_profile_name
  max_inactive_revisions       = 3
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [var.app_identities[each.key].id]
  }

  registry {
    server   = var.container_registry_login_server
    identity = var.app_identities[each.key].id
  }

  # A Key Vault reference, not a copy of the value into state. The identity here has to be the
  # same one the AcrPull grant went to, or the revision fails to start on secret resolution.
  dynamic "secret" {
    for_each = each.value.key_vault_secrets

    content {
      name                = secret.key
      identity            = var.app_identities[each.key].id
      key_vault_secret_id = secret.value
    }
  }

  # app_id is the string Dapr component scopes match on. It is deliberately the map key, so the
  # dapr-components module can read it back off this resource instead of guessing a convention.
  dapr {
    app_id       = each.key
    app_port     = each.value.target_port
    app_protocol = "http"
  }

  dynamic "ingress" {
    for_each = each.value.ingress == null ? [] : [each.value.ingress]

    content {
      external_enabled = ingress.value.external_enabled
      target_port      = each.value.target_port
      transport        = "auto"

      dynamic "ip_security_restriction" {
        for_each = ingress.value.allowed_ip_ranges

        content {
          name             = ip_security_restriction.key
          action           = "Allow"
          ip_address_range = ip_security_restriction.value
        }
      }

      traffic_weight {
        latest_revision = true
        percentage      = 100
      }
    }
  }

  template {
    min_replicas                     = each.value.min_replicas
    max_replicas                     = each.value.max_replicas
    polling_interval_in_seconds      = 30
    cooldown_period_in_seconds       = 300
    termination_grace_period_seconds = 30

    container {
      name   = each.key
      image  = each.value.image
      cpu    = each.value.cpu
      memory = each.value.memory

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = var.environment == "prod" ? "Production" : "Development"
      }

      # The client ID, not the principal ID. Both are GUIDs on the same identity and swapping
      # them fails at runtime with nothing to see at plan time.
      env {
        name  = "AZURE_CLIENT_ID"
        value = var.app_identities[each.key].client_id
      }

      liveness_probe {
        transport = "HTTP"
        port      = each.value.target_port
        path      = "/healthz/live"
      }

      readiness_probe {
        transport = "HTTP"
        port      = each.value.target_port
        path      = "/healthz/ready"
      }
    }

    dynamic "http_scale_rule" {
      for_each = each.value.ingress == null ? [] : [1]

      content {
        name                = "http-rps"
        concurrent_requests = "50"
      }
    }

    # identity_id on custom_scale_rule landed in azurerm 4.69.0. Below that floor a Service Bus
    # scale rule needs a connection string secret instead of the workload identity.
    dynamic "custom_scale_rule" {
      for_each = each.value.service_bus_scale_rule == null ? [] : [each.value.service_bus_scale_rule]

      content {
        name             = "sb-queue-depth"
        custom_rule_type = "azure-servicebus"
        identity_id      = var.app_identities[each.key].id

        metadata = {
          queueName    = custom_scale_rule.value.queue_name
          namespace    = custom_scale_rule.value.namespace
          messageCount = tostring(custom_scale_rule.value.message_count)
        }
      }
    }
  }

  lifecycle {
    precondition {
      condition     = each.value.ingress != null || each.value.service_bus_scale_rule != null
      error_message = "App ${each.key} has neither ingress nor a queue scale rule, so nothing can ever wake it."
    }
  }
}
