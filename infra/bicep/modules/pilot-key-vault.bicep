// ADR-0004: closed RBAC vault scaffold. It contains no secrets, keys, access
// policies or data roles. The owner must supply deletion/retention choices.
// Soft delete retains a deleted vault/name for 7–90 days; enabling purge
// protection is irreversible and prevents early purge during that interval.
@minLength(3)
@maxLength(24)
param keyVaultName string

@description('Tenant owning this vault. Supply the actual deployment tenant; no repository default.')
param tenantId string

@minValue(7)
@maxValue(90)
@description('Owner-approved soft-delete retention. This cannot be changed after creation.')
param softDeleteRetentionInDays int

@description('Explicit owner decision. Once enabled, purge protection cannot be disabled and prevents immediate cleanup.')
param enablePurgeProtection bool

param privateEndpointSubnetId string
param keyVaultPrivateDnsZoneId string

var location = 'eastus2'

resource vault 'Microsoft.KeyVault/vaults@2026-02-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    accessPolicies: []
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: false
    enableSoftDelete: true
    softDeleteRetentionInDays: softDeleteRetentionInDays
    enablePurgeProtection: enablePurgeProtection
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      bypass: 'None'
      defaultAction: 'Deny'
      ipRules: []
      virtualNetworkRules: []
    }
  }
}

resource vaultPrivateEndpoint 'Microsoft.Network/privateEndpoints@2025-05-01' = {
  name: '${keyVaultName}-pe'
  location: location
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'vault'
        properties: {
          privateLinkServiceId: vault.id
          groupIds: [
            'vault'
          ]
        }
      }
    ]
  }
}

resource vaultPrivateDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2025-05-01' = {
  parent: vaultPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'vault'
        properties: {
          privateDnsZoneId: keyVaultPrivateDnsZoneId
        }
      }
    ]
  }
}

output keyVaultId string = vault.id
output privateEndpointId string = vaultPrivateEndpoint.id
