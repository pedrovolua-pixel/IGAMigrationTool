// Connectivity only; no telemetry collection, SDK or diagnostic export.
// Use one AMPLS per isolated pilot network. PrivateOnly changes DNS access for
// all Azure Monitor clients in this linked VNet; verify no shared clients.
// https://learn.microsoft.com/azure/azure-monitor/fundamentals/private-link-configure
// https://learn.microsoft.com/azure/templates/microsoft.insights/2021-09-01/privatelinkscopes
@minLength(1)
param privateLinkScopeName string

@minLength(1)
param privateEndpointName string

@minLength(4)
@maxLength(63)
param workspaceName string

@minLength(1)
param applicationInsightsName string

@minLength(1)
param virtualNetworkName string

@description('Existing private-endpoint subnet at least /27, with at least 11 available AMPLS addresses beyond Azure reservations. DNS zones must already be linked to this isolated VNet.')
@minLength(1)
param privateEndpointSubnetName string

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2025-05-01' existing = {
  name: virtualNetworkName
}

resource privateEndpointSubnet 'Microsoft.Network/virtualNetworks/subnets@2025-05-01' existing = {
  parent: virtualNetwork
  name: privateEndpointSubnetName
}

resource workspace 'Microsoft.OperationalInsights/workspaces@2025-07-01' existing = {
  name: workspaceName
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: applicationInsightsName
}

var monitorZoneNames = [
  'privatelink.monitor.azure.com'
  'privatelink.oms.opinsights.azure.com'
  'privatelink.ods.opinsights.azure.com'
  'privatelink.agentsvc.azure-automation.net'
  'privatelink.blob.${environment().suffixes.storage}'
]

resource monitorZones 'Microsoft.Network/privateDnsZones@2024-06-01' existing = [
  for zoneName in monitorZoneNames: {
    name: zoneName
  }
]

resource privateLinkScope 'Microsoft.Insights/privateLinkScopes@2021-09-01' = {
  name: privateLinkScopeName
  location: 'global'
  properties: {
    accessModeSettings: {
      ingestionAccessMode: 'PrivateOnly'
      queryAccessMode: 'PrivateOnly'
      exclusions: []
    }
  }
}

resource workspaceLink 'Microsoft.Insights/privateLinkScopes/scopedResources@2021-09-01' = {
  parent: privateLinkScope
  name: 'workspace'
  properties: {
    linkedResourceId: workspace.id
  }
}

resource applicationInsightsLink 'Microsoft.Insights/privateLinkScopes/scopedResources@2021-09-01' = {
  parent: privateLinkScope
  name: 'application-insights'
  properties: {
    linkedResourceId: applicationInsights.id
  }
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2025-05-01' = {
  name: privateEndpointName
  location: 'eastus2'
  properties: {
    subnet: {
      id: privateEndpointSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: 'azure-monitor'
        properties: {
          privateLinkServiceId: privateLinkScope.id
          groupIds: ['azuremonitor']
        }
      }
    ]
  }
  dependsOn: [workspaceLink, applicationInsightsLink]
}

resource zoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2025-05-01' = {
  parent: privateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      for (zoneName, i) in monitorZoneNames: {
        name: replace(zoneName, '.', '-')
        properties: {
          privateDnsZoneId: monitorZones[i].id
        }
      }
    ]
  }
}

output privateLinkScopeResourceId string = privateLinkScope.id
output privateEndpointResourceId string = privateEndpoint.id
