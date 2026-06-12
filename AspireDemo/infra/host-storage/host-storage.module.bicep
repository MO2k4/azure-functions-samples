@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

resource host_storage 'Microsoft.Storage/storageAccounts@2024-01-01' = {
  name: take('hoststorage${uniqueString(resourceGroup().id)}', 24)
  kind: 'StorageV2'
  location: location
  sku: {
    name: 'Standard_GRS'
  }
  properties: {
    accessTier: 'Hot'
    allowSharedKeyAccess: false
    isHnsEnabled: false
    minimumTlsVersion: 'TLS1_2'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
  tags: {
    'aspire-resource-name': 'host-storage'
    'cost-center': 'GAZE'
    owner: 'AZE'
    environment: 'learning'
    project: 'aspire-demo'
  }
}

output blobEndpoint string = host_storage.properties.primaryEndpoints.blob

output dataLakeEndpoint string = host_storage.properties.primaryEndpoints.dfs

output queueEndpoint string = host_storage.properties.primaryEndpoints.queue

output tableEndpoint string = host_storage.properties.primaryEndpoints.table

output name string = host_storage.name

output id string = host_storage.id