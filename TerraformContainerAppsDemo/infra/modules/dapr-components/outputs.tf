output "state_store_name" {
  value       = azurerm_container_app_environment_dapr_component.state_cosmos.name
  description = "Component name the app passes to the Dapr state API."
}

output "pubsub_name" {
  value       = azurerm_container_app_environment_dapr_component.pubsub_servicebus.name
  description = "Component name the app passes to the Dapr pub/sub API."
}

output "blob_state_store_name" {
  value       = try(azurerm_container_app_environment_dapr_component.state_blob[0].name, null)
  description = "Component name of the optional Blob state store, null when blob_state_store is not set."
}
