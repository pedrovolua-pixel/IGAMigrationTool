// Optional access-only composition. Existing foundation is not recreated here.
// Mandatory protected snapshot/input validation and separately approved live session.
@minLength(1)
param existingVnetName string
@minLength(1)
param existingNatGatewayName string
@minLength(1)
param workstationSubnetName string
@minLength(1)
param workstationSubnetCidr string
@minLength(1)
param workstationPrivateAddress string
@minLength(1)
param workstationVmName string
@minLength(1)
param workstationNicName string
@minLength(1)
param workstationNsgName string
@minLength(1)
param bastionName string
@minLength(1)
param appIlbAddress string
@minLength(1)
param developerRdpSourceAddress string
@minLength(1)
param windowsImageVersion string
@minLength(1)
param adminUsername string
@secure()
@minLength(12)
param adminPassword string

@sealed()
type EgressRule = {
  name: string
  category: 'dns' | 'platform' | 'identity' | 'certificate' | 'update'
  destinationAddress: string
  protocol: 'Tcp' | 'Udp'
  destinationPort: int
}

@description('Exact approved host/port flows only; mandatory external validator verifies purpose mapping and protected endpoint exclusions.')
@minLength(1)
@maxLength(100)
param egressRules EgressRule[]

@minLength(1)
param environmentName string
@minLength(1)
param browserDnsLinkName string

resource environment 'Microsoft.App/managedEnvironments@2026-01-01' existing = {
  name: environmentName
}

module desktop 'modules/pilot-development-private-browser-access.bicep' = {
  name: 'private-development-desktop'
  params: {
    existingVnetName: existingVnetName
    existingNatGatewayName: existingNatGatewayName
    workstationSubnetName: workstationSubnetName
    workstationSubnetCidr: workstationSubnetCidr
    workstationPrivateAddress: workstationPrivateAddress
    workstationVmName: workstationVmName
    workstationNicName: workstationNicName
    workstationNsgName: workstationNsgName
    bastionName: bastionName
    appIlbAddress: appIlbAddress
    developerRdpSourceAddress: developerRdpSourceAddress
    windowsImageVersion: windowsImageVersion
    adminUsername: adminUsername
    adminPassword: adminPassword
    egressRules: egressRules
  }
}

module browserDns 'modules/pilot-development-browser-dns.bicep' = {
  name: 'private-development-browser-dns'
  params: {
    environmentDefaultDomain: environment.properties.defaultDomain
    environmentStaticIp: environment.properties.staticIp
    existingVnetName: existingVnetName
    virtualNetworkLinkName: browserDnsLinkName
  }
}
