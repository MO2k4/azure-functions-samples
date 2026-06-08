@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

resource orders_http_identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: take('orders_http_identity-${uniqueString(resourceGroup().id)}', 128)
  location: location
}

output id string = orders_http_identity.id

output clientId string = orders_http_identity.properties.clientId

output principalId string = orders_http_identity.properties.principalId

output principalName string = orders_http_identity.name

output name string = orders_http_identity.name