output "resource_group_name" {
  value       = azurerm_resource_group.this.name
  description = "Resource group holding everything this root module creates."
}

output "container_registry_login_server" {
  value       = azurerm_container_registry.this.login_server
  description = "Push the orders-api image here before the first apply, or the first revision has nothing to pull."
}

output "container_apps_environment_id" {
  value       = module.container_apps.environment_id
  description = "Container App Environment ID."
}

output "container_apps_environment_default_domain" {
  value       = module.container_apps.environment_default_domain
  description = "Default domain, which is also the name of the hand-built private DNS zone."
}

output "container_apps_environment_static_ip" {
  value       = module.container_apps.environment_static_ip
  description = "Internal load balancer address the wildcard and apex A records point at."
}

output "orders_api_fqdn" {
  value       = module.container_apps.app_fqdns[local.orders_app_id]
  description = "Internal ingress FQDN of the orders app. Point the APIM backend here."
}

output "dapr_app_ids" {
  value       = module.container_apps.dapr_app_ids
  description = "Dapr app ids as reported by the container app resources. The strings a component scope has to match."
}

output "app_identity_client_ids" {
  value       = module.identity.client_ids
  description = "Client IDs for azureClientId metadata and AZURE_CLIENT_ID env vars."
}

output "app_identity_principal_ids" {
  value       = module.identity.principal_ids
  description = "Principal IDs for role assignments made outside this configuration."
}

output "private_dns_zone_ids" {
  value       = module.networking.private_dns_zone_ids
  description = "Private DNS zone IDs keyed by service, for a hub VNet that needs its own links."
}
