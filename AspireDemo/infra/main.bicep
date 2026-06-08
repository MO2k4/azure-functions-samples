targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention, the name of the resource group for your application will use this name, prefixed with rg-')
param environmentName string

@minLength(1)
@description('The location used for all deployed resources')
param location string

@description('Id of the user or app to assign application roles')
param principalId string = ''

@metadata({azd: {
  type: 'generate'
  config: {length:22,noSpecial:true}
  }
})
@secure()
param cache_password string

var tags = {
  'azd-env-name': environmentName
}

resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module aca_env 'aca-env/aca-env.module.bicep' = {
  name: 'aca-env'
  scope: rg
  params: {
    aca_env_acr_outputs_name: aca_env_acr.outputs.name
    location: location
    userPrincipalId: principalId
  }
}
module aca_env_acr 'aca-env-acr/aca-env-acr.module.bicep' = {
  name: 'aca-env-acr'
  scope: rg
  params: {
    location: location
  }
}
module app_storage 'app-storage/app-storage.module.bicep' = {
  name: 'app-storage'
  scope: rg
  params: {
    location: location
  }
}
module host_storage 'host-storage/host-storage.module.bicep' = {
  name: 'host-storage'
  scope: rg
  params: {
    location: location
  }
}
module messaging 'messaging/messaging.module.bicep' = {
  name: 'messaging'
  scope: rg
  params: {
    location: location
  }
}
module orders_http_identity 'orders-http-identity/orders-http-identity.module.bicep' = {
  name: 'orders-http-identity'
  scope: rg
  params: {
    location: location
  }
}
module orders_http_roles_host_storage 'orders-http-roles-host-storage/orders-http-roles-host-storage.module.bicep' = {
  name: 'orders-http-roles-host-storage'
  scope: rg
  params: {
    host_storage_outputs_name: host_storage.outputs.name
    location: location
    principalId: orders_http_identity.outputs.principalId
  }
}
module orders_queue_identity 'orders-queue-identity/orders-queue-identity.module.bicep' = {
  name: 'orders-queue-identity'
  scope: rg
  params: {
    location: location
  }
}
module orders_queue_roles_host_storage 'orders-queue-roles-host-storage/orders-queue-roles-host-storage.module.bicep' = {
  name: 'orders-queue-roles-host-storage'
  scope: rg
  params: {
    host_storage_outputs_name: host_storage.outputs.name
    location: location
    principalId: orders_queue_identity.outputs.principalId
  }
}
module orders_sb_identity 'orders-sb-identity/orders-sb-identity.module.bicep' = {
  name: 'orders-sb-identity'
  scope: rg
  params: {
    location: location
  }
}
module orders_sb_roles_app_storage 'orders-sb-roles-app-storage/orders-sb-roles-app-storage.module.bicep' = {
  name: 'orders-sb-roles-app-storage'
  scope: rg
  params: {
    app_storage_outputs_name: app_storage.outputs.name
    location: location
    principalId: orders_sb_identity.outputs.principalId
  }
}
module orders_sb_roles_host_storage 'orders-sb-roles-host-storage/orders-sb-roles-host-storage.module.bicep' = {
  name: 'orders-sb-roles-host-storage'
  scope: rg
  params: {
    host_storage_outputs_name: host_storage.outputs.name
    location: location
    principalId: orders_sb_identity.outputs.principalId
  }
}
module orders_sb_roles_messaging 'orders-sb-roles-messaging/orders-sb-roles-messaging.module.bicep' = {
  name: 'orders-sb-roles-messaging'
  scope: rg
  params: {
    location: location
    messaging_outputs_name: messaging.outputs.name
    principalId: orders_sb_identity.outputs.principalId
  }
}
output ACA_ENV_AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = aca_env.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN
output ACA_ENV_AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = aca_env.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_ID
output ACA_ENV_AZURE_CONTAINER_REGISTRY_ENDPOINT string = aca_env.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output ACA_ENV_AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID string = aca_env.outputs.AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID
output APP_STORAGE_BLOBENDPOINT string = app_storage.outputs.blobEndpoint
output APP_STORAGE_QUEUEENDPOINT string = app_storage.outputs.queueEndpoint
output AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = aca_env.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = aca_env.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output HOST_STORAGE_BLOBENDPOINT string = host_storage.outputs.blobEndpoint
output HOST_STORAGE_DATALAKEENDPOINT string = host_storage.outputs.dataLakeEndpoint
output HOST_STORAGE_QUEUEENDPOINT string = host_storage.outputs.queueEndpoint
output HOST_STORAGE_TABLEENDPOINT string = host_storage.outputs.tableEndpoint
output MESSAGING_SERVICEBUSENDPOINT string = messaging.outputs.serviceBusEndpoint
output ORDERS_HTTP_IDENTITY_CLIENTID string = orders_http_identity.outputs.clientId
output ORDERS_HTTP_IDENTITY_ID string = orders_http_identity.outputs.id
output ORDERS_QUEUE_IDENTITY_CLIENTID string = orders_queue_identity.outputs.clientId
output ORDERS_QUEUE_IDENTITY_ID string = orders_queue_identity.outputs.id
output ORDERS_SB_IDENTITY_CLIENTID string = orders_sb_identity.outputs.clientId
output ORDERS_SB_IDENTITY_ID string = orders_sb_identity.outputs.id
