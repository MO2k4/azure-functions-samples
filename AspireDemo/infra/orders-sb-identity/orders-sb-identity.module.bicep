@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

resource orders_sb_identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: take('orders_sb_identity-${uniqueString(resourceGroup().id)}', 128)
  location: location
  tags: {
    'cost-center': 'GAZE'
    owner: 'AZE'
    environment: 'learning'
    project: 'aspire-demo'
  }
}

output id string = orders_sb_identity.id

output clientId string = orders_sb_identity.properties.clientId

output principalId string = orders_sb_identity.properties.principalId

output principalName string = orders_sb_identity.name

output name string = orders_sb_identity.name