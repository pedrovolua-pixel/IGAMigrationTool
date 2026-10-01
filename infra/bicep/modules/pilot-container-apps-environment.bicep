// G1 scaffold only: no application, image, sign-in, or public ingress.
// https://learn.microsoft.com/azure/templates/microsoft.app/2025-07-01/managedenvironments
// https://learn.microsoft.com/azure/container-apps/log-options
@minLength(1)
param environmentName string

@minLength(1)
param virtualNetworkName string

@description('Existing dedicated Microsoft.App/environments-delegated subnet with the verified NAT attachment.')
@minLength(1)
param containerAppsSubnetName string

@description('Explicit platform-managed resource group name; include its costs in the development spending boundary.')
@minLength(1)
param infrastructureResourceGroupName string

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2025-05-01' existing = {
  name: virtualNetworkName
}

resource containerAppsSubnet 'Microsoft.Network/virtualNetworks/subnets@2025-05-01' existing = {
  parent: virtualNetwork
  name: containerAppsSubnetName
}

resource environment 'Microsoft.App/managedEnvironments@2025-07-01' = {
  name: environmentName
  location: 'eastus2'
  properties: {
    infrastructureResourceGroup: infrastructureResourceGroupName
    publicNetworkAccess: 'Disabled'
    vnetConfiguration: {
      infrastructureSubnetId: containerAppsSubnet.id
      internal: true
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
    // No diagnostic setting is provisioned. Console/system log export remains
    // disabled until payload filtering, retention and private-path checks pass.
    appLogsConfiguration: {
      destination: 'azure-monitor'
    }
  }
}

output environmentResourceId string = environment.id
