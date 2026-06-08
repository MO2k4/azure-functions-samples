using './cache-containerapp.module.bicep'

param aca_env_outputs_azure_container_apps_environment_default_domain = '{{ .Env.ACA_ENV_AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN }}'
param aca_env_outputs_azure_container_apps_environment_id = '{{ .Env.ACA_ENV_AZURE_CONTAINER_APPS_ENVIRONMENT_ID }}'
param cache_password_value = '{{ securedParameter "cache_password" }}'
