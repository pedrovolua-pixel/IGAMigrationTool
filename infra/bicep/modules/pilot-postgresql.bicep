// Partial Milestone 1 scaffold. No customer database, SQL role, migration or
// workload grant is created. Managed-identity clients require separately
// authorized Entra database roles and deployed cross-customer denial tests.
// References: https://learn.microsoft.com/en-us/azure/templates/microsoft.dbforpostgresql/2025-08-01/flexibleservers
// https://learn.microsoft.com/en-us/azure/postgresql/network/concepts-networking-private-link
@minLength(3)
@maxLength(63)
param serverName string

@minLength(1)
param skuName string

@allowed(['Burstable', 'GeneralPurpose', 'MemoryOptimized'])
param skuTier string

@minValue(32)
param storageSizeGB int

@description('Explicit approved storage-growth choice. Enabled can increase storage charges; disabled requires capacity monitoring.')
@allowed(['Disabled', 'Enabled'])
param storageAutoGrow string

@description('Explicit approved rolling retention; this scaffold does not select a retention policy or prove deletion-safe restore.')
@minValue(7)
@maxValue(35)
param backupRetentionDays int

@description('Approved Entra administrator object ID. This is not automatically the deployer or a workload identity.')
@minLength(36)
@maxLength(36)
param administratorPrincipalId string

@minLength(1)
param administratorPrincipalName string

@allowed(['User', 'Group', 'ServicePrincipal'])
param administratorPrincipalType string

@description('Approved single-tenant Entra directory containing the administrator and workload principals.')
@minLength(36)
@maxLength(36)
param entraTenantId string

@description('Resource ID of the existing isolated private-endpoint subnet; no delegated database subnet is created.')
@minLength(1)
param privateEndpointSubnetId string

@description('Resource ID of an existing privatelink.postgres.database.azure.com zone linked to the workload VNet.')
@minLength(1)
param privateDnsZoneId string

var location = 'eastus2'

resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2025-08-01' = {
  name: serverName
  location: location
  sku: {
    name: skuName
    tier: skuTier
  }
  properties: {
    createMode: 'Default'
    version: '18'
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Disabled'
      tenantId: entraTenantId
    }
    highAvailability: {
      mode: 'Disabled'
    }
    backup: {
      backupRetentionDays: backupRetentionDays
      geoRedundantBackup: 'Disabled'
    }
    network: {
      publicNetworkAccess: 'Disabled'
    }
    storage: {
      storageSizeGB: storageSizeGB
      autoGrow: storageAutoGrow
      type: 'Premium_LRS'
    }
  }
}

resource administrator 'Microsoft.DBforPostgreSQL/flexibleServers/administrators@2025-08-01' = {
  parent: server
  name: administratorPrincipalId
  properties: {
    principalName: administratorPrincipalName
    principalType: administratorPrincipalType
    tenantId: entraTenantId
  }
  // Configuration changes can temporarily make Entra principal operations
  // unavailable. Wait for TLS configuration and the private DNS path first.
  dependsOn: [minimumTls, dnsZoneGroup]
}

resource secureTransport 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2025-08-01' = {
  parent: server
  name: 'require_secure_transport'
  properties: {
    value: 'ON'
    source: 'user-override'
  }
}

resource minimumTls 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2025-08-01' = {
  parent: server
  name: 'ssl_min_protocol_version'
  properties: {
    value: 'TLSv1.2'
    source: 'user-override'
  }
  dependsOn: [secureTransport]
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2025-05-01' = {
  name: '${serverName}-pe'
  location: location
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: '${serverName}-postgresql'
        properties: {
          privateLinkServiceId: server.id
          groupIds: ['postgresqlServer']
        }
      }
    ]
  }
}

resource dnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2025-05-01' = {
  parent: privateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'postgresql'
        properties: {
          privateDnsZoneId: privateDnsZoneId
        }
      }
    ]
  }
}

output serverResourceId string = server.id
output serverFqdn string = server.properties.fullyQualifiedDomainName
output privateEndpointResourceId string = privateEndpoint.id
