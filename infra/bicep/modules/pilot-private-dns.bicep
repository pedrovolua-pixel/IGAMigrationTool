// Azure public-cloud private endpoint zones for the accepted pilot services.
// Links an existing VNet; does not create networks or auto-register workloads.
@allowed([
  'privatelink.azurecr.io'
  'privatelink.vaultcore.azure.net'
  // Explicit Azure public-cloud zone; pilot is pinned to East US 2.
  #disable-next-line no-hardcoded-env-urls
  'privatelink.blob.core.windows.net'
  'privatelink.postgres.database.azure.com'
  'privatelink.monitor.azure.com'
  'privatelink.oms.opinsights.azure.com'
  'privatelink.ods.opinsights.azure.com'
  'privatelink.agentsvc.azure-automation.net'
])
param zoneName string

param virtualNetworkId string
param virtualNetworkLinkName string

resource zone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: zoneName
  location: 'global'
}

resource link 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: zone
  name: virtualNetworkLinkName
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetworkId
    }
  }
}

output privateDnsZoneId string = zone.id
output virtualNetworkLinkId string = link.id
