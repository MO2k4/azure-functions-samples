output "vnet_id" {
  value       = azurerm_virtual_network.this.id
  description = "Resource ID of the virtual network. The environment's own private DNS zone links against this."
}

output "container_apps_subnet_id" {
  value       = azurerm_subnet.container_apps.id
  description = "Subnet ID to pass as infrastructure_subnet_id on the Container App Environment."
}

output "container_apps_subnet_prefix" {
  value       = local.aca_prefix
  description = "CIDR of the delegated infrastructure subnet, for callers that need to write firewall rules against it."
}

output "private_endpoint_subnet_id" {
  value       = azurerm_subnet.private_endpoints.id
  description = "Subnet ID holding the private endpoints for Cosmos DB, Service Bus, Key Vault and the registry."
}

output "apim_subnet_id" {
  value       = azurerm_subnet.apim.id
  description = "Subnet ID for the API Management instance that fronts the environment."
}

output "network_security_group_id" {
  value       = azurerm_network_security_group.container_apps.id
  description = "NSG protecting the infrastructure subnet."
}

output "nsg_association_id" {
  value       = azurerm_subnet_network_security_group_association.container_apps.id
  description = "ID of the subnet to NSG association. Reference this to force the association ahead of the environment; Terraform infers no edge from the environment to it."
}

output "private_dns_zone_ids" {
  value       = { for key, zone in azurerm_private_dns_zone.this : key => zone.id }
  description = "Private DNS zone IDs keyed by service."
}
