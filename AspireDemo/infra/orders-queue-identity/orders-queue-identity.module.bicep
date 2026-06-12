@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

resource orders_queue_identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: take('orders_queue_identity-${uniqueString(resourceGroup().id)}', 128)
  location: location
  tags: {
    'cost-center': 'GAZE'
    owner: 'AZE'
    environment: 'learning'
    project: 'aspire-demo'
  }
}

output id string = orders_queue_identity.id

output clientId string = orders_queue_identity.properties.clientId

output principalId string = orders_queue_identity.properties.principalId

output principalName string = orders_queue_identity.name

output name string = orders_queue_identity.name