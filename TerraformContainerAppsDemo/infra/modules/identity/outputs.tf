output "identity_ids" {
  value       = { for id, identity in azurerm_user_assigned_identity.app : id => identity.id }
  description = "Resource IDs keyed by app id, for the identity, registry and secret blocks on the container app."
}

output "client_ids" {
  value       = { for id, identity in azurerm_user_assigned_identity.app : id => identity.client_id }
  description = "Client IDs keyed by app id. This is what Dapr's azureClientId metadata wants, not the principal ID."
}

output "principal_ids" {
  value       = { for id, identity in azurerm_user_assigned_identity.app : id => identity.principal_id }
  description = "Principal IDs keyed by app id, for role assignments made outside this module. Not interchangeable with client_ids."
}

output "acr_pull_role_assignment_ids" {
  value       = { for id, assignment in azurerm_role_assignment.acr_pull : id => assignment.id }
  description = "AcrPull grant IDs keyed by app id. Nothing on the container app references these, which is exactly the problem the depends_on in the root module solves."
}
