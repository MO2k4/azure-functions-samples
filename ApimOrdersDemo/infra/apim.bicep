// API Management in front of the order function: OpenAPI import, backend credential, product and one subscription.

param location string
param tags object
param resourceToken string

@allowed(['Consumption', 'BasicV2'])
param apimSku string

param publisherEmail string
param functionBaseUrl string

@secure()
param functionKey string

resource apim 'Microsoft.ApiManagement/service@2024-05-01' = {
  name: 'apim-orders-${resourceToken}'
  location: location
  tags: tags
  sku: {
    name: apimSku
    capacity: apimSku == 'Consumption' ? 0 : 1
  }
  properties: {
    publisherEmail: publisherEmail
    publisherName: 'Orders demo'
  }
}

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
    value: '''
<policies>
  <inbound>
    <base />
    <set-backend-service backend-id="orders-func" />
    <set-header name="Ocp-Apim-Subscription-Key" exists-action="delete" />
  </inbound>
  <backend>
    <base />
  </backend>
  <outbound>
    <base />
  </outbound>
  <on-error>
    <base />
  </on-error>
</policies>
'''
  }
  dependsOn: [ordersBackend]
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
output subscriptionId string = contosoShop.name
