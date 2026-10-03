// ADR-0010 optional local template. Never deploy without protected input validation,
// actual network/provider proof, owner credential handoff and priced session approval.
// Developer shared-source behavior remains unverified; no guessed source/default.
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

var location = 'eastus2'
var dnsServerAddresses = union(
  map(filter(egressRules, rule => rule.category == 'dns'), rule => rule.destinationAddress),
  []
)

resource existingVnet 'Microsoft.Network/virtualNetworks@2025-05-01' existing = {
  name: existingVnetName
}
resource existingNat 'Microsoft.Network/natGateways@2025-05-01' existing = {
  name: existingNatGatewayName
}

var essentialRules = [
  for (rule, index) in egressRules: {
    name: 'essential-${rule.name}'
    properties: {
      priority: 200 + index
      direction: 'Outbound'
      access: 'Allow'
      protocol: rule.protocol
      sourcePortRange: '*'
      destinationPortRange: string(rule.destinationPort)
      sourceAddressPrefix: workstationPrivateAddress
      destinationAddressPrefix: rule.destinationAddress
    }
  }
]
var platformDenies = [
  for (tag, index) in ['AzurePlatformDNS', 'AzurePlatformIMDS', 'AzurePlatformLKM']: {
    // Platform traffic can bypass ordinary IP/default NSG rules. No silent
    // exception: reviewed DNS hosts are explicitly bound on the NIC; licensing/platform
    // feasibility must be proved before deployment. WireServer is not NSG-filtered.
    name: 'deny-${tag}'
    properties: {
      priority: 3800 + index
      direction: 'Outbound'
      access: 'Deny'
      protocol: '*'
      sourcePortRange: '*'
      destinationPortRange: '*'
      sourceAddressPrefix: '*'
      destinationAddressPrefix: tag
    }
  }
]
var defaultDenies = [
  for direction in ['Inbound', 'Outbound']: {
    name: 'deny-all-${toLower(direction)}'
    properties: {
      priority: 4000
      direction: direction
      access: 'Deny'
      protocol: '*'
      sourcePortRange: '*'
      destinationPortRange: '*'
      sourceAddressPrefix: '*'
      destinationAddressPrefix: '*'
    }
  }
]

resource workstationNsg 'Microsoft.Network/networkSecurityGroups@2025-05-01' = {
  name: workstationNsgName
  location: location
  properties: {
    securityRules: concat(
      [
        {
          name: 'owner-developer-rdp'
          properties: {
            priority: 100
            direction: 'Inbound'
            access: 'Allow'
            protocol: 'Tcp'
            sourcePortRange: '*'
            destinationPortRange: '3389'
            sourceAddressPrefix: developerRdpSourceAddress
            destinationAddressPrefix: workstationPrivateAddress
          }
        }
        {
          name: 'private-app-https'
          properties: {
            priority: 100
            direction: 'Outbound'
            access: 'Allow'
            protocol: 'Tcp'
            sourcePortRange: '*'
            destinationPortRange: '443'
            sourceAddressPrefix: workstationPrivateAddress
            destinationAddressPrefix: appIlbAddress
          }
        }
      ],
      essentialRules,
      platformDenies,
      defaultDenies
    )
  }
}

resource workstationSubnet 'Microsoft.Network/virtualNetworks/subnets@2025-05-01' = {
  parent: existingVnet
  name: workstationSubnetName
  properties: {
    addressPrefix: workstationSubnetCidr
    defaultOutboundAccess: false
    delegations: []
    serviceEndpoints: []
    privateEndpointNetworkPolicies: 'Enabled'
    privateLinkServiceNetworkPolicies: 'Enabled'
    networkSecurityGroup: {
      id: workstationNsg.id
    }
    natGateway: {
      id: existingNat.id
    }
  }
}

resource workstationNic 'Microsoft.Network/networkInterfaces@2025-05-01' = {
  name: workstationNicName
  location: location
  properties: {
    enableIPForwarding: false
    enableAcceleratedNetworking: false
    networkSecurityGroup: {
      id: workstationNsg.id
    }
    dnsSettings: {
      dnsServers: dnsServerAddresses
    }
    ipConfigurations: [
      {
        name: 'private-workstation'
        properties: {
          primary: true
          privateIPAddressVersion: 'IPv4'
          privateIPAllocationMethod: 'Static'
          privateIPAddress: workstationPrivateAddress
          subnet: {
            id: workstationSubnet.id
          }
        }
      }
    ]
  }
}

resource workstation 'Microsoft.Compute/virtualMachines@2024-11-01' = {
  name: workstationVmName
  location: location
  properties: {
    hardwareProfile: {
      vmSize: 'Standard_B2s_v2'
    }
    storageProfile: {
      imageReference: {
        publisher: 'MicrosoftWindowsServer'
        offer: 'WindowsServer'
        sku: '2022-datacenter-g2'
        version: windowsImageVersion
      }
      osDisk: {
        createOption: 'FromImage'
        diskSizeGB: 128
        caching: 'ReadWrite'
        deleteOption: 'Delete'
        managedDisk: {
          storageAccountType: 'StandardSSD_LRS'
        }
      }
      dataDisks: []
    }
    osProfile: {
      computerName: workstationVmName
      adminUsername: adminUsername
      adminPassword: adminPassword
      allowExtensionOperations: false
    }
    networkProfile: {
      networkInterfaces: [
        {
          id: workstationNic.id
          properties: {
            primary: true
            deleteOption: 'Delete'
          }
        }
      ]
    }
    diagnosticsProfile: {
      bootDiagnostics: {
        enabled: false
      }
    }
  }
}

// Developer uses the existing VNet directly: no dedicated AzureBastionSubnet,
// public IP, paid SKU fallback or unsupported features are configured.
resource bastion 'Microsoft.Network/bastionHosts@2024-05-01' = {
  name: bastionName
  location: location
  sku: {
    name: 'Developer'
  }
  properties: {
    virtualNetwork: {
      id: existingVnet.id
    }
  }
}

output workstationResourceId string = workstation.id
output workstationNicResourceId string = workstationNic.id
output workstationNsgResourceId string = workstationNsg.id
output workstationSubnetResourceId string = workstationSubnet.id
output bastionResourceId string = bastion.id
