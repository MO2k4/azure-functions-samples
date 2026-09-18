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
  description = "Resource group that holds the identities."
}

variable "app_ids" {
  type        = set(string)
  description = "Dapr app ids. One user-assigned identity is created per entry, and the container-apps module is keyed on the same set."
  nullable    = false

  validation {
    condition     = length(var.app_ids) > 0
    error_message = "app_ids must not be empty."
  }

  validation {
    condition     = alltrue([for id in var.app_ids : can(regex("^[a-z][a-z0-9-]{1,30}[a-z0-9]$", id))])
    error_message = "App ids become identity names and Dapr app ids; use lower-case alphanumeric and hyphens, 3-32 chars."
  }
}

variable "container_registry_id" {
  type        = string
  description = "Registry the apps pull from. Scope of the AcrPull grant."
}

variable "key_vault_id" {
  type        = string
  description = "Key Vault holding the app secrets. Scope of the Key Vault Secrets User grant."
}

variable "service_bus_queue_ids" {
  type        = map(string)
  default     = {}
  nullable    = false
  description = "Queue or topic resource IDs keyed by name. Grants are scoped here, never at the namespace."
}

variable "cosmosdb_account_id" {
  type        = string
  description = "Cosmos account ID. Used to build the data-plane role definition and scope paths."
}

variable "cosmosdb_account_name" {
  type        = string
  description = "Cosmos account name. azurerm_cosmosdb_sql_role_assignment takes the name, not the ID."
}

variable "cosmosdb_resource_group_name" {
  type        = string
  description = "Resource group of the Cosmos account."
}

variable "cosmosdb_database_name" {
  type        = string
  description = "Database the data-plane grant is scoped to."
}

variable "tags" {
  type     = map(string)
  default  = {}
  nullable = false
}
