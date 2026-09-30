// Partial Milestone 1 module. Static egress addresses must be verified in the
// deployed Container Apps/NAT path before this namespace carries pilot work.
@minLength(6)
@maxLength(50)
param namespaceName string

@minLength(1)
@description('Name of the existing static IPv4 public IP used by the pilot application NAT path in this resource group.')
param staticEgressPublicIpName string

var location = 'eastus2'

resource staticEgressPublicIp 'Microsoft.Network/publicIPAddresses@2025-05-01' existing = {
  name: staticEgressPublicIpName
}

resource serviceBus 'Microsoft.ServiceBus/namespaces@2026-01-01' = {
  name: namespaceName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    disableLocalAuth: true
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource networkRules 'Microsoft.ServiceBus/namespaces/networkRuleSets@2026-01-01' = {
  parent: serviceBus
  name: 'default'
  properties: {
    defaultAction: 'Deny'
    publicNetworkAccess: 'Enabled'
    ipRules: [{
      ipMask: staticEgressPublicIp.properties.ipAddress
      action: 'Allow'
    }]
    virtualNetworkRules: []
  }
}

output namespaceResourceId string = serviceBus.id
