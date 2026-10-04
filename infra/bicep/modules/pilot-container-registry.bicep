// ADR-0004: private image distribution only. No credentials, image retention
// policy, role assignments, tasks or public build access are created here.
@minLength(5)
@maxLength(50)
param registryName string

param privateEndpointSubnetId string
param registryPrivateDnsZoneId string

var location = 'eastus2'

resource registry 'Microsoft.ContainerRegistry/registries@2025-11-01' = {
  name: registryName
  location: location
  sku: {
    name: 'Premium'
  }
  properties: {
    adminUserEnabled: false
    anonymousPullEnabled: false
    publicNetworkAccess: 'Disabled'
    networkRuleBypassOptions: 'None'
    networkRuleBypassAllowedForTasks: false
    networkRuleSet: {
      defaultAction: 'Deny'
      ipRules: []
    }
    dataEndpointEnabled: true
  }
}

resource registryPrivateEndpoint 'Microsoft.Network/privateEndpoints@2025-05-01' = {
  name: '${registryName}-pe'
  location: location
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'registry'
        properties: {
          privateLinkServiceId: registry.id
          groupIds: [
            'registry'
          ]
        }
      }
    ]
  }
}

resource registryPrivateDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2025-05-01' = {
  parent: registryPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'registry'
        properties: {
          privateDnsZoneId: registryPrivateDnsZoneId
        }
      }
    ]
  }
}

output registryId string = registry.id
output loginServer string = registry.properties.loginServer
output privateEndpointId string = registryPrivateEndpoint.id
