// Partial Milestone 1 network foundation. CIDR allocation and provider
// availability require environment-specific verification before deployment.
@minLength(1)
param virtualNetworkName string

@description('Non-secret private CIDR for the pilot virtual network.')
@minLength(1)
param virtualNetworkAddressPrefix string

@minLength(1)
param containerAppsSubnetName string

@description('Non-secret, dedicated Container Apps workload-profile subnet CIDR, /27 or larger.')
@minLength(1)
param containerAppsSubnetAddressPrefix string

@minLength(1)
param privateEndpointSubnetName string

@description('Non-secret private-endpoint subnet CIDR, disjoint from the Container Apps subnet.')
@minLength(1)
param privateEndpointSubnetAddressPrefix string

@minLength(1)
param staticEgressPublicIpName string

@minLength(1)
param natGatewayName string

var location = 'eastus2'

resource staticEgressPublicIp 'Microsoft.Network/publicIPAddresses@2025-05-01' = {
  name: staticEgressPublicIpName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Regional'
  }
  properties: {
    publicIPAddressVersion: 'IPv4'
    publicIPAllocationMethod: 'Static'
  }
}

resource natGateway 'Microsoft.Network/natGateways@2025-05-01' = {
  name: natGatewayName
  location: location
  sku: {
    name: 'Standard'
  }
  properties: {
    idleTimeoutInMinutes: 10
    publicIpAddresses: [{
      id: staticEgressPublicIp.id
    }]
  }
}

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2025-05-01' = {
  name: virtualNetworkName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [virtualNetworkAddressPrefix]
    }
  }
}

resource containerAppsSubnet 'Microsoft.Network/virtualNetworks/subnets@2025-05-01' = {
  parent: virtualNetwork
  name: containerAppsSubnetName
  properties: {
    addressPrefix: containerAppsSubnetAddressPrefix
    delegations: [{
      name: 'container-apps-environment'
      properties: {
        serviceName: 'Microsoft.App/environments'
      }
    }]
    natGateway: {
      id: natGateway.id
    }
  }
}

resource privateEndpointSubnet 'Microsoft.Network/virtualNetworks/subnets@2025-05-01' = {
  parent: virtualNetwork
  name: privateEndpointSubnetName
  properties: {
    addressPrefix: privateEndpointSubnetAddressPrefix
    privateEndpointNetworkPolicies: 'Disabled'
  }
}

output containerAppsSubnetId string = containerAppsSubnet.id
output privateEndpointSubnetId string = privateEndpointSubnet.id
output staticEgressPublicIpId string = staticEgressPublicIp.id
