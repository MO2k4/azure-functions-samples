output "environment_id" {
  value       = azurerm_container_app_environment.this.id
  description = "Container App Environment ID; Dapr components attach to this."
}

output "environment_default_domain" {
  value       = azurerm_container_app_environment.this.default_domain
  description = "Default domain of the environment. Internal FQDNs are <app>.internal.<default_domain>. This zone is not created for you."
}

output "environment_static_ip" {
  value       = azurerm_container_app_environment.this.static_ip_address
  description = "Static IP of the environment, internal when internal_load_balancer_enabled is true. Target of the wildcard and apex A records in the default_domain zone."
}

output "app_fqdns" {
  value       = { for id, app in azurerm_container_app.this : id => try(app.ingress[0].fqdn, null) }
  description = "Ingress FQDN per app, null for apps without ingress. APIM backends point here."
}

# Reading this off the resource rather than off keys(var.apps) is the whole point. It is what
# makes a component scope an attribute reference, so the two strings cannot drift apart.
output "dapr_app_ids" {
  value       = { for id, app in azurerm_container_app.this : id => app.dapr[0].app_id }
  description = "Dapr app ids read back off the container app resource. This is what the scopes list on a Dapr component expects; the container app name is not."
}
