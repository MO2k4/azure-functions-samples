variable "workload" {
  type        = string
  description = "Short workload name used in resource names."
}

variable "environment" {
  type        = string
  description = "Environment short name."

  validation {
    condition     = contains(["dev", "test", "prod"], var.environment)
    error_message = "environment must be one of dev, test, prod."
  }
}

variable "location" {
  type        = string
  description = "Azure region."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group that holds the environment and the apps."
}

variable "log_analytics_workspace_id" {
  type        = string
  description = "Workspace ID; required because logs_destination is log-analytics. In v5 logs_destination is no longer computed, so an upgraded config that only sets this silently drops to Streaming Only."
}

variable "infrastructure_subnet_id" {
  type        = string
  description = "Delegated subnet for the Container Apps control plane. Immutable: changing it recreates the environment."
}

variable "internal_only" {
  type        = bool
  default     = true
  description = "Internal load balancer mode; the environment gets no public IP. Immutable. Inbound NSG rules mean nothing on an external environment because traffic never enters your subnet."
}

variable "dapr_application_insights_connection_string" {
  type        = string
  default     = null
  sensitive   = true
  description = "Sidecar telemetry target. Immutable: changing it recreates the environment."
}

variable "container_registry_login_server" {
  type        = string
  description = "Login server of the registry the apps pull from."
}

variable "app_identities" {
  type = map(object({
    id        = string
    client_id = string
  }))
  nullable    = false
  description = "User-assigned identities keyed by app id, produced by the identity module. This module does not create identities so that grants and apps can have separate lifetimes."
}

variable "dedicated_workload_profiles" {
  type = map(object({
    profile_type  = string
    minimum_count = optional(number, 0)
    maximum_count = optional(number, 3)
  }))
  default     = {}
  nullable    = false
  description = "Dedicated profiles keyed by profile name. A Consumption profile is always added, because removing the last profile recreates the environment."

  validation {
    condition = alltrue([
      for name, profile in var.dedicated_workload_profiles :
      can(regex("^(D4|D8|D16|D32|E4|E8|E16|E32)$", profile.profile_type))
    ])
    error_message = "profile_type must be one of D4, D8, D16, D32, E4, E8, E16, E32."
  }

  validation {
    condition = alltrue([
      for name, profile in var.dedicated_workload_profiles :
      profile.maximum_count >= profile.minimum_count
    ])
    error_message = "maximum_count must be greater than or equal to minimum_count."
  }

  validation {
    condition     = !contains(keys(var.dedicated_workload_profiles), "Consumption")
    error_message = "The Consumption profile is added by the module; do not declare it here."
  }
}

variable "apps" {
  type = map(object({
    image                 = string
    target_port           = number
    cpu                   = optional(number, 0.5)
    memory                = optional(string, "1Gi")
    min_replicas          = optional(number, 1)
    max_replicas          = optional(number, 10)
    workload_profile_name = optional(string, "Consumption")
    key_vault_secrets     = optional(map(string), {})

    ingress = optional(object({
      external_enabled  = optional(bool, false)
      allowed_ip_ranges = optional(map(string), {})
    }))

    service_bus_scale_rule = optional(object({
      queue_name    = string
      namespace     = string
      message_count = optional(number, 5)
    }))
  }))
  nullable    = false
  description = "Apps keyed by Dapr app id. The map key becomes the dapr app_id and the container app name suffix, so a Dapr component can never be scoped to a string that does not exist."

  validation {
    condition     = alltrue([for id, app in var.apps : can(regex("^[a-z][a-z0-9-]{1,30}[a-z0-9]$", id))])
    error_message = "App keys become Dapr app ids and container app names; use lower-case alphanumeric and hyphens, 3-32 chars."
  }

  validation {
    condition     = alltrue([for id, app in var.apps : app.min_replicas <= app.max_replicas])
    error_message = "min_replicas must be less than or equal to max_replicas."
  }

  validation {
    condition     = alltrue([for id, app in var.apps : contains([0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0], app.cpu)])
    error_message = "cpu must be one of the allocations the Consumption profile accepts: 0.25 through 2.0 in 0.25 steps."
  }

  validation {
    condition = alltrue([
      for id, app in var.apps :
      app.workload_profile_name == "Consumption" || contains(keys(var.dedicated_workload_profiles), app.workload_profile_name)
    ])
    error_message = "workload_profile_name must be Consumption or a key of dedicated_workload_profiles."
  }

  # KEDA's namespace metadata is the bare name. Dapr's namespaceName, two modules over, is the
  # FQDN. Same concept, two formats, and neither one fails at plan time.
  validation {
    condition = alltrue([
      for id, app in var.apps :
      app.service_bus_scale_rule == null || !strcontains(try(app.service_bus_scale_rule.namespace, ""), ".")
    ])
    error_message = "The Service Bus scale rule namespace is the bare namespace name, not the FQDN. Drop the .servicebus.windows.net suffix."
  }
}

variable "tags" {
  type     = map(string)
  default  = {}
  nullable = false
}
