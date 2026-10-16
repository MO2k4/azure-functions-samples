targetScope = 'resourceGroup'

@description('azd environment name; feeds the resource name suffix.')
param environmentName string

param location string = resourceGroup().location

@description('Consumption: $0 idle, per-call billing, cold first call. BasicV2: one warm unit, ~$150/month. Neither can be changed into the other in place.')
@allowed(['Consumption', 'BasicV2'])
param apimSku string = 'BasicV2'

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

module entra 'entra.bicep' = {
  name: 'entra'
  params: {
    resourceToken: resourceToken
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
    ordersApiAppId: entra.outputs.ordersApiAppId
    backendAppId: entra.outputs.backendAppId
    testClientAppId: entra.outputs.testClientAppId
    appInsightsId: appInsights.id
    appInsightsInstrumentationKey: appInsights.properties.InstrumentationKey
  }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' existing = {
  name: 'func-orders-${resourceToken}'
}

// Easy Auth in front of the function: only tokens for the backend app registration, and only
// from API Management's managed identity (oid claim). Requests without a token get a 401, tokens from
// any other identity a 403, before the code runs.
resource easyAuth 'Microsoft.Web/sites/config@2024-04-01' = {
  parent: functionApp
  name: 'authsettingsV2'
  properties: {
    platform: { enabled: true }
    globalValidation: {
      requireAuthentication: true
      unauthenticatedClientAction: 'Return401'
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          clientId: entra.outputs.backendAppId
          openIdIssuer: '${environment().authentication.loginEndpoint}${tenant().tenantId}/v2.0'
        }
        validation: {
          allowedAudiences: [entra.outputs.backendAppId]
          defaultAuthorizationPolicy: {
            allowedPrincipals: {
              identities: [apim.outputs.principalId]
            }
          }
        }
      }
    }
  }
}

output FUNCTION_APP_NAME string = function.outputs.name
output FUNCTION_BASE_URL string = function.outputs.baseUrl
output APIM_NAME string = apim.outputs.name
output APIM_GATEWAY_URL string = apim.outputs.gatewayUrl
output APIM_SUBSCRIPTION_ID string = apim.outputs.subscriptionId
output APPINSIGHTS_NAME string = appInsights.name
output ORDERS_API_APP_ID string = entra.outputs.ordersApiAppId
output BACKEND_APP_ID string = entra.outputs.backendAppId
output TEST_CLIENT_APP_ID string = entra.outputs.testClientAppId
output AZURE_TENANT_ID string = tenant().tenantId
