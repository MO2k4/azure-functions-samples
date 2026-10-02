targetScope = 'resourceGroup'

@description('azd environment name; feeds the resource name suffix.')
param environmentName string

param location string = resourceGroup().location

@description('Consumption: $0 idle, per-call billing, cold first call. BasicV2: one warm unit, ~$150/month. Neither can be changed into the other in place.')
@allowed(['Consumption', 'BasicV2'])
param apimSku string = 'Consumption'

param publisherEmail string

@secure()
param apimFunctionKey string = newGuid()

// Every resource needs these four tags in the subscription this sample was built on;
// a tag policy denies the deployment otherwise. Change the values to fit yours.
param tags object = {
  'cost-center': 'GAZE'
  owner: 'AZE'
  environment: 'learning'
  project: 'apim-demo'
}

var resourceToken = take(uniqueString(subscription().id, resourceGroup().id, environmentName), 8)

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-orders-${resourceToken}'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-orders-${resourceToken}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
  }
}

module function 'function.bicep' = {
  name: 'function'
  params: {
    location: location
    tags: tags
    resourceToken: resourceToken
    apimFunctionKey: apimFunctionKey
    appInsightsConnectionString: appInsights.properties.ConnectionString
  }
}

module apim 'apim.bicep' = {
  name: 'apim'
  params: {
    location: location
    tags: tags
    resourceToken: resourceToken
    apimSku: apimSku
    publisherEmail: publisherEmail
    functionBaseUrl: function.outputs.baseUrl
    functionKey: apimFunctionKey
    appInsightsId: appInsights.id
    appInsightsInstrumentationKey: appInsights.properties.InstrumentationKey
  }
}

output FUNCTION_APP_NAME string = function.outputs.name
output FUNCTION_BASE_URL string = function.outputs.baseUrl
output APIM_NAME string = apim.outputs.name
output APIM_GATEWAY_URL string = apim.outputs.gatewayUrl
output APIM_SUBSCRIPTION_ID string = apim.outputs.subscriptionId
output APPINSIGHTS_NAME string = appInsights.name
