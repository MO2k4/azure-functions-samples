locals {
  aca_prefix  = cidrsubnet(var.address_space, var.aca_subnet_newbits, 0)
  pe_prefix   = cidrsubnet(var.address_space, 8, 8)
  apim_prefix = cidrsubnet(var.address_space, 8, 9)

  # ACR Premium needs the region-specific data zone as well as the registry zone.
  # Without it the manifest resolves privately and the layer download falls back to the public endpoint.
  private_dns_zone_names = {
    cosmos     = "privatelink.documents.azure.com"
    servicebus = "privatelink.servicebus.windows.net"
    keyvault   = "privatelink.vaultcore.azure.net"
    acr        = "privatelink.azurecr.io"
    acr_data   = "${var.location}.data.privatelink.azurecr.io"
  }
}

resource "azurerm_virtual_network" "this" {
  name                = "vnet-${var.workload}-${var.environment}"
  location            = var.location
  resource_group_name = var.resource_group_name
  address_space       = [var.address_space]
  tags                = var.tags
}

# Size is immutable once the environment exists. Changing this prefix is a rebuild of the
# environment and of every app and Dapr component in it.
resource "azurerm_subnet" "container_apps" {
  name                 = "snet-aca-infra"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.this.name
  address_prefixes     = [local.aca_prefix]

  delegation {
    name = "aca-environment"

    service_delegation {
      name    = "Microsoft.App/environments"
      actions = ["Microsoft.Network/virtualNetworks/subnets/action"]
    }
  }
}

resource "azurerm_subnet" "private_endpoints" {
  name                              = "snet-pe"
  resource_group_name               = var.resource_group_name
  virtual_network_name              = azurerm_virtual_network.this.name
  address_prefixes                  = [local.pe_prefix]
  private_endpoint_network_policies = "Disabled"
}

resource "azurerm_subnet" "apim" {
  name                 = "snet-apim"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.this.name
  address_prefixes     = [local.apim_prefix]

  delegation {
    name = "apim"

    service_delegation {
      name = "Microsoft.ApiManagement/service"
    }
  }

  # v5 shape. v4 wrote service_endpoints = ["Microsoft.Storage", "Microsoft.EventHub"].
  service_endpoint {
    service = "Microsoft.Storage"
  }

  service_endpoint {
    service = "Microsoft.EventHub"
  }
}

# Workload profiles rules, not the Consumption-only set. The Consumption environment needs
# an outbound allow to AzureCloud on 443 and to 1.1.1.1/1.0.0.1 on 53; a workload profiles
# environment reaches its control plane over the same 443 rules the app already needs.
resource "azurerm_network_security_group" "container_apps" {
  name                = "nsg-aca-${var.workload}-${var.environment}"
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags

  security_rule {
    name                       = "allow-apim-to-edge-proxy"
    priority                   = 100
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_ranges    = ["443", "31443"]
    source_address_prefix      = local.apim_prefix
    destination_address_prefix = local.aca_prefix
  }

  security_rule {
    name                       = "allow-lb-health-probe"
    priority                   = 110
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "30000-32767"
    source_address_prefix      = "AzureLoadBalancer"
    destination_address_prefix = local.aca_prefix
  }

  # An NSG is evaluated on both NICs, so the catch-all deny below would otherwise override the
  # default AllowVnetInBound at priority 65000 and break pod to pod traffic inside the subnet.
  security_rule {
    name                       = "allow-intra-subnet-inbound"
    priority                   = 120
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "*"
    source_port_range          = "*"
    destination_port_range     = "*"
    source_address_prefix      = local.aca_prefix
    destination_address_prefix = local.aca_prefix
  }

  security_rule {
    name                       = "deny-other-inbound"
    priority                   = 4000
    direction                  = "Inbound"
    access                     = "Deny"
    protocol                   = "*"
    source_port_range          = "*"
    destination_port_range     = "*"
    source_address_prefix      = "*"
    destination_address_prefix = "*"
  }

  security_rule {
    name                       = "allow-intra-subnet-outbound"
    priority                   = 100
    direction                  = "Outbound"
    access                     = "Allow"
    protocol                   = "*"
    source_port_range          = "*"
    destination_port_range     = "*"
    source_address_prefix      = local.aca_prefix
    destination_address_prefix = local.aca_prefix
  }

  security_rule {
    name                       = "allow-mcr"
    priority                   = 110
    direction                  = "Outbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "443"
    source_address_prefix      = local.aca_prefix
    destination_address_prefix = "MicrosoftContainerRegistry"
  }

  security_rule {
    name                       = "allow-frontdoor-firstparty"
    priority                   = 120
    direction                  = "Outbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "443"
    source_address_prefix      = local.aca_prefix
    destination_address_prefix = "AzureFrontDoor.FirstParty"
  }

  security_rule {
    name                       = "allow-entra"
    priority                   = 130
    direction                  = "Outbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "443"
    source_address_prefix      = local.aca_prefix
    destination_address_prefix = "AzureActiveDirectory"
  }

  security_rule {
    name                       = "allow-azure-monitor"
    priority                   = 140
    direction                  = "Outbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "443"
    source_address_prefix      = local.aca_prefix
    destination_address_prefix = "AzureMonitor"
  }

  # Never deny 168.63.129.16. The environment stops working, and nothing in the portal says why.
  security_rule {
    name                       = "allow-azure-platform-dns"
    priority                   = 150
    direction                  = "Outbound"
    access                     = "Allow"
    protocol                   = "*"
    source_port_range          = "*"
    destination_port_range     = "53"
    source_address_prefix      = local.aca_prefix
    destination_address_prefix = "168.63.129.16"
  }

  security_rule {
    name                       = "allow-private-endpoints"
    priority                   = 160
    direction                  = "Outbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "443"
    source_address_prefix      = local.aca_prefix
    destination_address_prefix = local.pe_prefix
  }

  security_rule {
    name                       = "deny-other-outbound"
    priority                   = 4000
    direction                  = "Outbound"
    access                     = "Deny"
    protocol                   = "*"
    source_port_range          = "*"
    destination_port_range     = "*"
    source_address_prefix      = "*"
    destination_address_prefix = "*"
  }
}

resource "azurerm_subnet_network_security_group_association" "container_apps" {
  subnet_id                 = azurerm_subnet.container_apps.id
  network_security_group_id = azurerm_network_security_group.container_apps.id
}

resource "azurerm_private_dns_zone" "this" {
  for_each = local.private_dns_zone_names

  name                = each.value
  resource_group_name = var.resource_group_name
  tags                = var.tags
}

# private_dns_zone_group on the endpoint writes the records. It does not link the zone to the
# VNet, and an unlinked zone resolves to the public IP with no DNS error anywhere.
resource "azurerm_private_dns_zone_virtual_network_link" "this" {
  for_each = azurerm_private_dns_zone.this

  name                 = "link-${each.key}-${var.workload}-${var.environment}"
  private_dns_zone_id  = each.value.id
  virtual_network_id   = azurerm_virtual_network.this.id
  registration_enabled = false
  tags                 = var.tags
}

resource "azurerm_private_endpoint" "cosmos" {
  name                = "pe-cosmos-${var.workload}-${var.environment}"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = azurerm_subnet.private_endpoints.id
  tags                = var.tags

  private_service_connection {
    name                           = "psc-cosmos"
    private_connection_resource_id = var.cosmosdb_account_id
    subresource_names              = ["Sql"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "cosmos"
    private_dns_zone_ids = [azurerm_private_dns_zone.this["cosmos"].id]
  }
}

resource "azurerm_private_endpoint" "servicebus" {
  name                = "pe-sb-${var.workload}-${var.environment}"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = azurerm_subnet.private_endpoints.id
  tags                = var.tags

  private_service_connection {
    name                           = "psc-sb"
    private_connection_resource_id = var.servicebus_namespace_id
    subresource_names              = ["namespace"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "servicebus"
    private_dns_zone_ids = [azurerm_private_dns_zone.this["servicebus"].id]
  }
}

resource "azurerm_private_endpoint" "key_vault" {
  name                = "pe-kv-${var.workload}-${var.environment}"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = azurerm_subnet.private_endpoints.id
  tags                = var.tags

  private_service_connection {
    name                           = "psc-kv"
    private_connection_resource_id = var.key_vault_id
    subresource_names              = ["vault"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "keyvault"
    private_dns_zone_ids = [azurerm_private_dns_zone.this["keyvault"].id]
  }
}

resource "azurerm_private_endpoint" "container_registry" {
  name                = "pe-acr-${var.workload}-${var.environment}"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = azurerm_subnet.private_endpoints.id
  tags                = var.tags

  private_service_connection {
    name                           = "psc-acr"
    private_connection_resource_id = var.container_registry_id
    subresource_names              = ["registry"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name = "acr"
    private_dns_zone_ids = [
      azurerm_private_dns_zone.this["acr"].id,
      azurerm_private_dns_zone.this["acr_data"].id,
    ]
  }
}
