// Flex Consumption function app that serves the order API. Identity-based storage, no connection strings.

param location string
param tags object
param resourceToken string

@secure()
@description('Value of the function key API Management sends as x-functions-key.')
param apimFunctionKey string

param appInsightsConnectionString string

var storageBlobDataOwner = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var deploymentContainer = 'deployments'

resource storage 'Microsoft.Storage/storageAccounts@2024-01-01' = {
  name: 'stapim${resourceToken}'
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: {
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
  }

  resource blob 'blobServices' = {
    name: 'default'

    resource deployments 'containers' = {
      name: deploymentContainer
    }
  }
}

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: 'plan-orders-${resourceToken}'
  location: location
  tags: tags
  kind: 'functionapp'
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: 'func-orders-${resourceToken}'
  location: location
  tags: union(tags, { 'azd-service-name': 'orders-api' })
  kind: 'functionapp,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'AzureWebJobsStorage__accountName', value: storage.name }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
      ]
    }
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storage.properties.primaryEndpoints.blob}${deploymentContainer}'
          authentication: { type: 'SystemAssignedIdentity' }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 40
        instanceMemoryMB: 2048
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
    }
  }
}

resource blobOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, functionApp.id, storageBlobDataOwner)
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwner)
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// A named key instead of listKeys(): API Management gets the value as a deployment input,
// so it never has to read secrets back from the Functions host.
#disable-next-line BCP081 // the Bicep type library has no schema for host keys; ARM accepts it
resource apimKey 'Microsoft.Web/sites/host/functionKeys@2024-04-01' = {
  name: '${functionApp.name}/default/apim'
  properties: {
    name: 'apim'
    value: apimFunctionKey
  }
  dependsOn: [blobOwner]
}

output name string = functionApp.name
output baseUrl string = 'https://${functionApp.properties.defaultHostName}/api'
