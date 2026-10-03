// Optional private-browser DNS only. Existing environment must pass independent
// internal/Disabled/same-VNet preflight; this template never changes its ingress.
// https://learn.microsoft.com/azure/container-apps/private-endpoints-with-dns
@minLength(1)
param environmentDefaultDomain string
@minLength(1)
param environmentStaticIp string
@minLength(1)
param existingVnetName string
@minLength(1)
param virtualNetworkLinkName string

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2025-05-01' existing = {
  name: existingVnetName
}
resource zone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: environmentDefaultDomain
  location: 'global'
}
resource link 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: zone
  name: virtualNetworkLinkName
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}
resource wildcard 'Microsoft.Network/privateDnsZones/A@2024-06-01' = {
  parent: zone
  name: '*'
  properties: {
    ttl: 60
    aRecords: [{ ipv4Address: environmentStaticIp }]
  }
}
resource apex 'Microsoft.Network/privateDnsZones/A@2024-06-01' = {
  parent: zone
  name: '@'
  properties: {
    ttl: 60
    aRecords: [{ ipv4Address: environmentStaticIp }]
  }
}
output privateDnsZoneId string = zone.id
output virtualNetworkLinkId string = link.id
