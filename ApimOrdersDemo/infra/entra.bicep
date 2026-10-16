// Entra ID app registrations: the API partners call, the function backend API Management calls,
// and a test client with the Orders.ReadWrite app role. Needs permission to register applications.

extension microsoftGraphV1

param resourceToken string

// v2 access tokens: aud is the client ID, iss is https://login.microsoftonline.com/{tenant}/v2.0.
var tokenVersion = 2
var ordersRoleId = guid('orders-api', resourceToken, 'Orders.ReadWrite')

resource ordersApiApp 'Microsoft.Graph/applications@v1.0' = {
  uniqueName: 'orders-api-${resourceToken}'
  displayName: 'Orders API (${resourceToken})'
  signInAudience: 'AzureADMyOrg'
  api: {
    requestedAccessTokenVersion: tokenVersion
  }
  appRoles: [
    {
      id: ordersRoleId
      value: 'Orders.ReadWrite'
      displayName: 'Orders.ReadWrite'
      description: 'Place and read orders through API Management.'
      allowedMemberTypes: ['Application']
      isEnabled: true
    }
  ]
}

resource ordersApiSp 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: ordersApiApp.appId
}

// A separate audience for the backend: a partner token for the gateway is useless against the function.
resource backendApp 'Microsoft.Graph/applications@v1.0' = {
  uniqueName: 'orders-backend-${resourceToken}'
  displayName: 'Orders backend (${resourceToken})'
  signInAudience: 'AzureADMyOrg'
  api: {
    requestedAccessTokenVersion: tokenVersion
  }
}

resource backendSp 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: backendApp.appId
}

// Stands in for a partner's service. Graph Bicep can't create secrets: see the README for the one-liner.
resource testClientApp 'Microsoft.Graph/applications@v1.0' = {
  uniqueName: 'orders-test-client-${resourceToken}'
  displayName: 'Orders test client (${resourceToken})'
  signInAudience: 'AzureADMyOrg'
}

resource testClientSp 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: testClientApp.appId
}

resource testClientRole 'Microsoft.Graph/appRoleAssignedTo@v1.0' = {
  appRoleId: ordersRoleId
  principalId: testClientSp.id
  resourceId: ordersApiSp.id
}

output ordersApiAppId string = ordersApiApp.appId
output backendAppId string = backendApp.appId
output testClientAppId string = testClientApp.appId
