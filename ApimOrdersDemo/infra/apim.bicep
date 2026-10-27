// API Management in front of the order function: OpenAPI import, managed identity plus function key
// toward the backend, Entra ID token validation for callers, two API versions in a version set, two
// products (free and partners), one subscription, and the developer portal.

param location string
param tags object
param resourceToken string

@allowed(['Consumption', 'BasicV2'])
param apimSku string

param publisherEmail string
param functionBaseUrl string

@secure()
param functionKey string

@description('Client ID of the app registration callers get tokens for (validate-azure-ad-token audience).')
param ordersApiAppId string

@description('Client ID of the backend app registration (authentication-managed-identity resource, Easy Auth audience).')
param backendAppId string

@description('Client ID of the only caller app validate-azure-ad-token accepts.')
param testClientAppId string

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
    // v2 tiers create the instance with the portal off; Consumption has no portal at all.
    developerPortalStatus: apimSku == 'Consumption' ? null : 'Enabled'
  }
}

// The default portal host; add a custom portal domain here too, or the try-it console gets a CORS error there.
var portalOrigin = 'https://${apim.name}.developer.azure-api.net'

// Plain (non-secret) values the policies read as {{name}}: IDs and the portal origin, not credentials.
var policyNamedValues = {
  'tenant-id': tenant().tenantId
  'orders-api-app-id': ordersApiAppId
  'orders-backend-app-id': backendAppId
  'orders-test-client-app-id': testClientAppId
  'portal-origin': portalOrigin
}

resource namedValues 'Microsoft.ApiManagement/service/namedValues@2024-05-01' = [for nv in items(policyNamedValues): {
  parent: apim
  name: nv.key
  properties: {
    displayName: nv.key
    value: nv.value
  }
}]

resource functionKeyValue 'Microsoft.ApiManagement/service/namedValues@2024-05-01' = {
  parent: apim
  name: 'orders-function-key'
  properties: {
    displayName: 'orders-function-key'
    secret: true
    value: functionKey
  }
}

resource ordersBackend 'Microsoft.ApiManagement/service/backends@2024-05-01' = {
  parent: apim
  name: 'orders-func'
  properties: {
    url: functionBaseUrl
    protocol: 'http'
    credentials: {
      header: {
        'x-functions-key': ['{{${functionKeyValue.name}}}']
      }
    }
  }
}

// Path versioning (Segment in ARM), fixed for every version in the set by the first one.
resource ordersVersionSet 'Microsoft.ApiManagement/service/apiVersionSets@2024-05-01' = {
  parent: apim
  name: 'orders'
  properties: {
    displayName: 'Orders API'
    versioningScheme: 'Segment'
  }
}

// The Original version: in the set, but no apiVersion, so existing callers keep /sales/orders.
// Giving it apiVersion: 'v1' would move it to /sales/v1/orders and break every existing call.
resource ordersApi 'Microsoft.ApiManagement/service/apis@2024-05-01' = {
  parent: apim
  name: 'orders'
  properties: {
    displayName: 'Orders API'
    apiVersionSetId: ordersVersionSet.id
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

// v2 breaks one contract: list-orders returns a page envelope. Same path, identifier in the URL: /sales/v2/orders.
resource ordersApiV2 'Microsoft.ApiManagement/service/apis@2024-05-01' = {
  parent: apim
  name: 'orders-v2'
  properties: {
    displayName: 'Orders API'
    apiVersionSetId: ordersVersionSet.id
    apiVersion: 'v2'
    apiVersionDescription: 'list-orders returns a page of orders ({ items, nextLink }) instead of an array.'
    path: 'sales'
    protocols: ['https']
    format: 'openapi'
    value: loadTextContent('../openapi/orders-v2.yaml')
    serviceUrl: functionBaseUrl
    subscriptionRequired: true
  }
  // Two APIs on one path only work inside a version set, so the Original has to join it first.
  dependsOn: [ordersApi]
}

// Same token check, backend and limits as the Original: a new version shouldn't be a way around Part 3.
resource ordersApiV2Policy 'Microsoft.ApiManagement/service/apis/policies@2024-05-01' = {
  parent: ordersApiV2
  name: 'policy'
  properties: {
    format: 'rawxml'
    value: loadTextContent('policies/api-orders.xml')
  }
  dependsOn: [ordersBackend, namedValues]
}

resource listOrdersV2 'Microsoft.ApiManagement/service/apis/operations@2024-05-01' existing = {
  parent: ordersApiV2
  name: 'list-orders'
}

resource listOrdersV2Policy 'Microsoft.ApiManagement/service/apis/operations/policies@2024-05-01' = {
  parent: listOrdersV2
  name: 'policy'
  properties: {
    format: 'rawxml'
    value: loadTextContent('policies/operation-list-orders-v2.xml')
  }
}

// One file per scope under infra/policies. Each narrower scope pulls in the broader one with <base />.
resource globalPolicy 'Microsoft.ApiManagement/service/policies@2024-05-01' = {
  parent: apim
  name: 'policy'
  properties: {
    format: 'rawxml'
    value: loadTextContent('policies/global.xml')
  }
  dependsOn: [namedValues]
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

// Partners: approval required, so a portal sign-up waits in `submitted` until someone approves it.
resource partnerProduct 'Microsoft.ApiManagement/service/products@2024-05-01' = {
  parent: apim
  name: 'orders-partners'
  properties: {
    displayName: 'Orders for partners'
    description: 'Order intake for partner shops. One subscription per partner, approved by the API team.'
    subscriptionRequired: true
    approvalRequired: true
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

// Evaluation: auto-approved, one subscription per user account, low limits.
resource freeProduct 'Microsoft.ApiManagement/service/products@2024-05-01' = {
  parent: apim
  name: 'orders-free'
  properties: {
    displayName: 'Orders (free)'
    description: 'Try the order API: 5 calls a minute, 1,000 a week. Keys are issued right away.'
    subscriptionRequired: true
    approvalRequired: false
    subscriptionsLimit: 1
    state: 'published'
  }
}

resource freeProductPolicy 'Microsoft.ApiManagement/service/products/policies@2024-05-01' = {
  parent: freeProduct
  name: 'policy'
  properties: {
    format: 'rawxml'
    value: loadTextContent('policies/product-orders-free.xml')
  }
}

// Every version is its own API, so each one goes into each product.
var orderApiVersions = [ordersApi.name, ordersApiV2.name]

resource partnerProductApis 'Microsoft.ApiManagement/service/products/apis@2024-05-01' = [for api in orderApiVersions: {
  parent: partnerProduct
  name: api
}]

resource freeProductApis 'Microsoft.ApiManagement/service/products/apis@2024-05-01' = [for api in orderApiVersions: {
  parent: freeProduct
  name: api
}]

resource partnersGroup 'Microsoft.ApiManagement/service/groups@2024-05-01' = {
  parent: apim
  name: 'partners'
  properties: {
    displayName: 'Partners'
    description: 'Partner developers who may see and request the partner product.'
  }
}

// Visibility in the portal: signed-in developers see the free product, members of `partners` the partner one.
resource freeProductDevelopers 'Microsoft.ApiManagement/service/products/groups@2024-05-01' = {
  parent: freeProduct
  name: 'developers'
}

resource partnerProductPartners 'Microsoft.ApiManagement/service/products/groups@2024-05-01' = {
  parent: partnerProduct
  name: partnersGroup.name
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
