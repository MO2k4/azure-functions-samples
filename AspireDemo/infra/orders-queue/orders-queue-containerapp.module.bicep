@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param aca_env_outputs_azure_container_apps_environment_default_domain string

param aca_env_outputs_azure_container_apps_environment_id string

param orders_queue_containerimage string

param orders_queue_identity_outputs_id string

param host_storage_outputs_blobendpoint string

param host_storage_outputs_queueendpoint string

param host_storage_outputs_tableendpoint string

param host_storage_outputs_datalakeendpoint string

param orders_queue_identity_outputs_clientid string

param aca_env_outputs_azure_container_registry_endpoint string

param aca_env_outputs_azure_container_registry_managed_identity_id string

resource orders_queue 'Microsoft.App/containerApps@2025-10-02-preview' = {
  name: 'orders-queue'
  location: location
  properties: {
    configuration: {
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
          image: orders_queue_containerimage
          name: 'orders-queue'
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
              name: 'AZURE_CLIENT_ID'
              value: orders_queue_identity_outputs_clientid
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
      '${orders_queue_identity_outputs_id}': { }
      '${aca_env_outputs_azure_container_registry_managed_identity_id}': { }
    }
  }
  kind: 'functionapp'
}