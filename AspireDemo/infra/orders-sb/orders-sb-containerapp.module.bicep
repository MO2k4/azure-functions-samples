@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param aca_env_outputs_azure_container_apps_environment_default_domain string

param aca_env_outputs_azure_container_apps_environment_id string

param orders_sb_containerimage string

param orders_sb_identity_outputs_id string

param host_storage_outputs_blobendpoint string

param host_storage_outputs_queueendpoint string

param host_storage_outputs_tableendpoint string

param host_storage_outputs_datalakeendpoint string

param messaging_outputs_servicebusendpoint string

param app_storage_outputs_blobendpoint string

param app_storage_outputs_queueendpoint string

@secure()
param cache_password_value string

param orders_sb_identity_outputs_clientid string

param aca_env_outputs_azure_container_registry_endpoint string

param aca_env_outputs_azure_container_registry_managed_identity_id string

resource orders_sb 'Microsoft.App/containerApps@2025-10-02-preview' = {
  name: 'orders-sb'
  location: location
  properties: {
    configuration: {
      secrets: [
        {
          name: 'connectionstrings--cache'
          value: 'cache:6379,password=${cache_password_value}'
        }
        {
          name: 'cache-password'
          value: cache_password_value
        }
        {
          name: 'cache-uri'
          value: 'redis://:${uriComponent(cache_password_value)}@cache:6379'
        }
      ]
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: 8080
        transport: 'http'
      }
      registries: [
        {
          server: aca_env_outputs_azure_container_registry_endpoint
          identity: aca_env_outputs_azure_container_registry_managed_identity_id
        }
      ]
      runtime: {
        dotnet: {
          autoConfigureDataProtection: true
        }
      }
    }
    environmentId: aca_env_outputs_azure_container_apps_environment_id
    template: {
      containers: [
        {
          image: orders_sb_containerimage
          name: 'orders-sb'
          env: [
            {
              name: 'OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY'
              value: 'in_memory'
            }
            {
              name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
              value: 'true'
            }
            {
              name: 'FUNCTIONS_WORKER_RUNTIME'
              value: 'dotnet-isolated'
            }
            {
              name: 'AzureFunctionsJobHost__telemetryMode'
              value: 'OpenTelemetry'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'AzureWebJobsStorage__blobServiceUri'
              value: host_storage_outputs_blobendpoint
            }
            {
              name: 'AzureWebJobsStorage__queueServiceUri'
              value: host_storage_outputs_queueendpoint
            }
            {
              name: 'AzureWebJobsStorage__tableServiceUri'
              value: host_storage_outputs_tableendpoint
            }
            {
              name: 'AzureWebJobsStorage__dataLakeServiceUri'
              value: host_storage_outputs_datalakeendpoint
            }
            {
              name: 'Aspire__Azure__Storage__Blobs__AzureWebJobsStorage__ServiceUri'
              value: host_storage_outputs_blobendpoint
            }
            {
              name: 'Aspire__Azure__Storage__Queues__AzureWebJobsStorage__ServiceUri'
              value: host_storage_outputs_queueendpoint
            }
            {
              name: 'Aspire__Azure__Data__Tables__AzureWebJobsStorage__ServiceUri'
              value: host_storage_outputs_tableendpoint
            }
            {
              name: 'Aspire__Azure__Storage__Files__DataLake__AzureWebJobsStorage__ServiceUri'
              value: host_storage_outputs_datalakeendpoint
            }
            {
              name: 'messaging__fullyQualifiedNamespace'
              value: messaging_outputs_servicebusendpoint
            }
            {
              name: 'Aspire__Azure__Messaging__ServiceBus__messaging__FullyQualifiedNamespace'
              value: messaging_outputs_servicebusendpoint
            }
            {
              name: 'receipts__blobServiceUri'
              value: app_storage_outputs_blobendpoint
            }
            {
              name: 'receipts__queueServiceUri'
              value: app_storage_outputs_queueendpoint
            }
            {
              name: 'Aspire__Azure__Storage__Blobs__receipts__ServiceUri'
              value: app_storage_outputs_blobendpoint
            }
            {
              name: 'ConnectionStrings__cache'
              secretRef: 'connectionstrings--cache'
            }
            {
              name: 'CACHE_HOST'
              value: 'cache'
            }
            {
              name: 'CACHE_PORT'
              value: '6379'
            }
            {
              name: 'CACHE_PASSWORD'
              secretRef: 'cache-password'
            }
            {
              name: 'CACHE_URI'
              secretRef: 'cache-uri'
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: orders_sb_identity_outputs_clientid
            }
            {
              name: 'AZURE_TOKEN_CREDENTIALS'
              value: 'ManagedIdentityCredential'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
      }
    }
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${orders_sb_identity_outputs_id}': { }
      '${aca_env_outputs_azure_container_registry_managed_identity_id}': { }
    }
  }
  kind: 'functionapp'
}