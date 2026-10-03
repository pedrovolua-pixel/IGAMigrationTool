// ADR-0011 HTTPS-P01 environment shell only. No application or public admission.
// Direct use still requires protected provider validation and a priced owner session.
@minLength(1)
param environmentName string
@minLength(1)
param virtualNetworkName string
@minLength(1)
param containerAppsSubnetName string
@minLength(1)
param natGatewayName string
@minLength(1)
param infrastructureResourceGroupName string

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2025-05-01' existing = {
  name: virtualNetworkName
}
resource subnet 'Microsoft.Network/virtualNetworks/subnets@2025-05-01' existing = {
  parent: virtualNetwork
  name: containerAppsSubnetName
}
resource natGateway 'Microsoft.Network/natGateways@2025-05-01' existing = {
  name: natGatewayName
}

var networkValid = (toLower(virtualNetwork.location) == 'eastus2' && toLower(natGateway.location) == 'eastus2' && natGateway.sku.name == 'Standard' && length(natGateway.properties.?publicIpAddresses ?? []) == 1 && length(natGateway.properties.?publicIpPrefixes ?? []) == 0 && subnet.properties.?natGateway.?id == natGateway.id && length(subnet.properties.?delegations ?? []) == 1 && length(filter(
  subnet.properties.?delegations ?? [],
  delegation => delegation.properties.serviceName == 'Microsoft.App/environments'
)) == 1 && length(subnet.properties.?ipConfigurations ?? []) == 0 && length(subnet.properties.?privateEndpoints ?? []) == 0 && length(subnet.properties.?serviceAssociationLinks ?? []) == 0)
var reviewedSubnetId = networkValid
  ? subnet.id
  : fail('Require an unused dedicated East US 2 Microsoft.App/environments subnet with the exact existing Standard NAT attachment.')
var managedGroupName = toLower(infrastructureResourceGroupName) != toLower(resourceGroup().name)
  ? infrastructureResourceGroupName
  : fail('Require a distinct explicitly reviewed platform-managed resource group.')

resource environment 'Microsoft.App/managedEnvironments@2026-01-01' = {
  name: environmentName
  location: 'eastus2'
  properties: {
    infrastructureResourceGroup: managedGroupName
    publicNetworkAccess: 'Disabled'
    vnetConfiguration: {
      infrastructureSubnetId: reviewedSubnetId
      internal: false
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
    // No export, shared key, diagnostics setting or telemetry store is created.
    appLogsConfiguration: {
      destination: 'none'
    }
  }
}

output environmentResourceId string = environment.id
