variable "subscription_id" {
  type        = string
  description = "Target subscription. Required for plan and apply, not for validate."

  validation {
    condition     = can(regex("^[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$", var.subscription_id))
    error_message = "subscription_id must be a GUID."
  }
}

variable "workload" {
  type        = string
  default     = "orders"
  description = "Short workload name used in every resource name."

  validation {
    condition     = can(regex("^[a-z][a-z0-9]{1,10}$", var.workload))
    error_message = "workload must be 2-11 lower-case alphanumeric chars; it feeds a storage-style registry name with no separators."
  }
}

variable "environment" {
  type        = string
  description = "Environment short name. Also one of the four required tags."

  validation {
    condition     = contains(["dev", "test", "prod"], var.environment)
    error_message = "environment must be one of dev, test, prod."
  }
}

variable "location" {
  type        = string
  default     = "westeurope"
  description = "Azure region, compact form."

  validation {
    condition     = can(regex("^[a-z0-9]+$", var.location))
    error_message = "location must be the compact region name, for example westeurope."
  }
}

variable "address_space" {
  type        = string
  default     = "10.60.0.0/16"
  description = "VNet address space."

  validation {
    condition     = can(cidrhost(var.address_space, 0))
    error_message = "address_space must be valid CIDR notation."
  }
}

variable "cost_center" {
  type        = string
  description = "cost-center tag. The management group policy denies any resource without it."
}

variable "owner" {
  type        = string
  description = "owner tag."
}

variable "project" {
  type        = string
  description = "project tag."
}

variable "image_tag" {
  type        = string
  description = "Tag of the orders-api image in the registry. Never latest: a Single revision mode app needs a changing image reference to roll."

  validation {
    condition     = var.image_tag != "latest"
    error_message = "Use an immutable tag or a digest, not latest."
  }
}

variable "min_replicas" {
  type        = number
  default     = 1
  description = "Floor for the orders app. Set above 1 in prod so a rollout never drops to zero."

  validation {
    condition     = var.min_replicas >= 0 && var.min_replicas <= 300
    error_message = "min_replicas must be between 0 and 300."
  }
}

variable "max_replicas" {
  type        = number
  default     = 10
  description = "Ceiling for the orders app."

  validation {
    condition     = var.max_replicas >= 1 && var.max_replicas <= 300
    error_message = "max_replicas must be between 1 and 300."
  }
}

variable "dedicated_workload_profiles" {
  type = map(object({
    profile_type  = string
    minimum_count = optional(number, 0)
    maximum_count = optional(number, 3)
  }))
  default     = {}
  nullable    = false
  description = "Dedicated profiles in addition to Consumption. Adding the first profile to an environment that has none recreates the environment, so decide on day one."
}

variable "log_retention_days" {
  type        = number
  default     = 30
  description = "Log Analytics retention."

  validation {
    condition     = var.log_retention_days >= 30 && var.log_retention_days <= 730
    error_message = "log_retention_days must be between 30 and 730."
  }
}

variable "orders_api_key" {
  type        = string
  sensitive   = true
  description = "Placeholder downstream credential, stored in Key Vault and referenced by the app through a secret block. Supply it from the pipeline, not from a tfvars file in git."
}
