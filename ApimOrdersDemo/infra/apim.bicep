// API Management in front of the order function: OpenAPI import, managed identity toward the backend,
// Entra ID token validation for callers, product and one subscription.

param location string
param tags object
param resourceToken string

@allowed(['Consumption', 'BasicV2'])
param apimSku string

param publisherEmail string
param functionBaseUrl string

@description('Client ID of the app registration callers get tokens for (validate-azure-ad-token audience).')
param ordersApiAppId string

@description('Client ID of the backend app registration (authentication-managed-identity resource, Easy Auth audience).')
param backendAppId string

param appInsightsId string
param appInsightsInstrumentationKey string

resource apim 'Microsoft.ApiManagement/service@2024-05-01' = {
  name: 'apim-orders-${resourceToken}'
  location: location
  tags: tags
  sku: {
    name: apimSku
    capacity: apimSku == 'Consumption' ? 0 : 1
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    publisherEmail: publisherEmail
    publisherName: 'Orders demo'
  }
}

// Plain (non-secret) values the API policy reads as {{name}}: IDs, not credentials.
var policyNamedValues = {
  'tenant-id': tenant().tenantId
  'orders-api-app-id': ordersApiAppId
  'orders-backend-app-id': backendAppId
}

resource namedValues 'Microsoft.ApiManagement/service/namedValues@2024-05-01' = [for nv in items(policyNamedValues): {
  parent: apim
  name: nv.key
  properties: {
    displayName: nv.key
    value: nv.value
  }
}]

resource ordersBackend 'Microsoft.ApiManagement/service/backends@2024-05-01' = {
  parent: apim
  name: 'orders-func'
  properties: {
    url: functionBaseUrl
    protocol: 'http'
  }
}

resource ordersApi 'Microsoft.ApiManagement/service/apis@2024-05-01' = {
  parent: apim
  name: 'orders'
  properties: {
    displayName: 'Orders API'
    path: 'sales'
    protocols: ['https']
    format: 'openapi'
    value: loadTextContent('../openapi/orders.yaml')
    serviceUrl: functionBaseUrl
    subscriptionRequired: true
  }
}

resource ordersApiPolicy 'Microsoft.ApiManagement/service/apis/policies@2024-05-01' = {
  parent: ordersApi
  name: 'policy'
  properties: {
    format: 'rawxml'
    value: loadTextContent('policies/api-orders.xml')
  }
  dependsOn: [ordersBackend, namedValues]
}

// One file per scope under infra/policies. Each narrower scope pulls in the broader one with <base />.
resource globalPolicy 'Microsoft.ApiManagement/service/policies@2024-05-01' = {
  parent: apim
  name: 'policy'
  properties: {
    format: 'rawxml'
    value: loadTextContent('policies/global.xml')
  }
}

resource getOrder 'Microsoft.ApiManagement/service/apis/operations@2024-05-01' existing = {
  parent: ordersApi
  name: 'get-order'
}

// cache-lookup uses the built-in cache, which Consumption doesn't have (BasicV2 does).
resource getOrderPolicy 'Microsoft.ApiManagement/service/apis/operations/policies@2024-05-01' = {
  parent: getOrder
  name: 'policy'
  properties: {
    format: 'rawxml'
    value: loadTextContent('policies/operation-get-order.xml')
  }
}

resource appInsightsLogger 'Microsoft.ApiManagement/service/loggers@2024-05-01' = {
  parent: apim
  name: 'appinsights'
  properties: {
    loggerType: 'applicationInsights'
    resourceId: appInsightsId
    credentials: {
      instrumentationKey: appInsightsInstrumentationKey
    }
  }
}

// 100% sampling so every cold call shows up: the request row carries the gateway's total time,
// the dependency row the backend call, and the difference is what the gateway itself spent.
resource ordersApiDiagnostics 'Microsoft.ApiManagement/service/apis/diagnostics@2024-05-01' = {
  parent: ordersApi
  name: 'applicationinsights'
  properties: {
    loggerId: appInsightsLogger.id
    alwaysLog: 'allErrors'
    httpCorrelationProtocol: 'W3C'
    verbosity: 'information'
    sampling: {
      samplingType: 'fixed'
      percentage: 100
    }
  }
}

resource partnerProduct 'Microsoft.ApiManagement/service/products@2024-05-01' = {
  parent: apim
  name: 'orders-partners'
  properties: {
    displayName: 'Orders for partners'
    description: 'Order intake for partner shops. One subscription per partner.'
    subscriptionRequired: true
    approvalRequired: false
    state: 'published'
  }
}

resource partnerProductPolicy 'Microsoft.ApiManagement/service/products/policies@2024-05-01' = {
  parent: partnerProduct
  name: 'policy'
  properties: {
    format: 'rawxml'
    value: loadTextContent('policies/product-orders-partners.xml')
  }
}

resource partnerProductOrders 'Microsoft.ApiManagement/service/products/apis@2024-05-01' = {
  parent: partnerProduct
  name: ordersApi.name
}

resource contosoShop 'Microsoft.ApiManagement/service/subscriptions@2024-05-01' = {
  parent: apim
  name: 'contoso-shop'
  properties: {
    displayName: 'Contoso Shop'
    scope: '/products/${partnerProduct.name}'
    state: 'active'
  }
}

output name string = apim.name
output gatewayUrl string = apim.properties.gatewayUrl
output principalId string = apim.identity.principalId
output subscriptionId string = contosoShop.name
