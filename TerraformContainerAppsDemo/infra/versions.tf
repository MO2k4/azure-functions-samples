terraform {
  required_version = ">= 1.16.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "5.6.0"
    }
  }

  # Partial configuration. The rest comes from envs/<env>.backend.hcl at init time:
  #   terraform init -reconfigure -backend-config=envs/prod.backend.hcl
  backend "azurerm" {}
}

provider "azurerm" {
  features {}

  subscription_id = var.subscription_id

  # v5 defaults this to "none". On a fresh subscription the first apply then fails on an
  # unregistered Microsoft.App, and the error reads like a permissions problem.
  resource_provider_registrations = "legacy"
}
