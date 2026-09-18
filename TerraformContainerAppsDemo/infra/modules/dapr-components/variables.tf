variable "container_app_environment_id" {
  type        = string
  description = "Environment the components attach to."
}

variable "dapr_identity_client_id" {
  type        = string
  description = "Client ID of the user-assigned identity the sidecar uses to reach Cosmos DB, Blob and Service Bus. The client ID, not the principal ID."
}

variable "cosmos_endpoint" {
  type        = string
  description = "Cosmos account URL."

  validation {
    condition     = startswith(var.cosmos_endpoint, "https://")
    error_message = "cosmos_endpoint must be the account URL, for example https://acct.documents.azure.com:443/."
  }
}

variable "cosmos_database" {
  type        = string
  description = "Database the state store writes to."
}

variable "cosmos_container" {
  type        = string
  description = "Container the state store writes to."
}

variable "service_bus_namespace_fqdn" {
  type        = string
  description = "Fully qualified Service Bus namespace. Dapr's namespaceName is a FQDN; KEDA's namespace, one module over, is the bare name."

  validation {
    condition     = endswith(var.service_bus_namespace_fqdn, ".servicebus.windows.net")
    error_message = "service_bus_namespace_fqdn must be the fully qualified namespace, not the bare name."
  }
}

variable "blob_state_store" {
  type = object({
    account_name   = string
    container_name = string
  })
  default     = null
  description = "Optional second state store on Blob storage, used here for actor checkpoints."
}

variable "state_store_scopes" {
  type        = list(string)
  description = "Dapr app ids allowed to load the state store. Not container app names, and not Terraform resource names."

  validation {
    condition     = length(var.state_store_scopes) > 0
    error_message = "Set scopes explicitly; an unscoped component is loaded by every Dapr-enabled app in the environment."
  }
}

variable "pubsub_scopes" {
  type        = list(string)
  description = "Dapr app ids allowed to load the pub/sub component."

  validation {
    condition     = length(var.pubsub_scopes) > 0
    error_message = "Set scopes explicitly; an unscoped component is loaded by every Dapr-enabled app in the environment."
  }
}
