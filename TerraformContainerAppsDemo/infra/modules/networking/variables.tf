variable "workload" {
  type        = string
  description = "Short workload name used in resource names."

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]{1,14}$", var.workload))
    error_message = "workload must be 2-15 chars, lower-case alphanumeric or hyphen, starting with a letter."
  }
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
  description = "Azure region. Also used to build the region-specific ACR data private DNS zone name."

  validation {
    condition     = can(regex("^[a-z0-9]+$", var.location))
    error_message = "location must be the compact region name, for example westeurope, not \"West Europe\"."
  }
}

variable "resource_group_name" {
  type        = string
  description = "Resource group that holds the network."
}

variable "address_space" {
  type        = string
  description = "VNet address space in CIDR notation."

  validation {
    condition     = can(cidrhost(var.address_space, 0))
    error_message = "address_space must be valid CIDR notation, for example 10.60.0.0/16."
  }

  validation {
    condition     = tonumber(split("/", var.address_space)[1]) <= 20
    error_message = "address_space must be /20 or larger to leave room for the Container Apps infrastructure subnet."
  }
}

variable "aca_subnet_newbits" {
  type        = number
  default     = 7
  description = "Bits added to address_space to size the Container Apps infrastructure subnet. 7 on a /16 gives the /23 this demo designs for."

  validation {
    condition     = var.aca_subnet_newbits >= 1 && var.aca_subnet_newbits <= 16
    error_message = "aca_subnet_newbits must be between 1 and 16."
  }

  validation {
    condition     = tonumber(split("/", var.address_space)[1]) + var.aca_subnet_newbits <= 27
    error_message = "The Container Apps infrastructure subnet must be /27 or larger for workload profiles; reduce aca_subnet_newbits."
  }
}

variable "cosmosdb_account_id" {
  type        = string
  description = "Cosmos DB account to put behind a private endpoint."
}

variable "servicebus_namespace_id" {
  type        = string
  description = "Service Bus namespace to put behind a private endpoint. Must be Premium; Standard has no Private Link support."
}

variable "key_vault_id" {
  type        = string
  description = "Key Vault to put behind a private endpoint."
}

variable "container_registry_id" {
  type        = string
  description = "Container registry to put behind a private endpoint. Must be Premium."
}

variable "tags" {
  type        = map(string)
  default     = {}
  description = "Tags applied to every resource in this module."
  nullable    = false
}
