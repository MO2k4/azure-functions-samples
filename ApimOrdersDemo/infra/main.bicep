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

module function 'function.bicep' = {
  name: 'function'
  params: {
    location: location
    tags: tags
    resourceToken: resourceToken
    apimFunctionKey: apimFunctionKey
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
  }
}

output FUNCTION_APP_NAME string = function.outputs.name
output FUNCTION_BASE_URL string = function.outputs.baseUrl
output APIM_NAME string = apim.outputs.name
output APIM_GATEWAY_URL string = apim.outputs.gatewayUrl
output APIM_SUBSCRIPTION_ID string = apim.outputs.subscriptionId
