// Development infrastructure only. Two disclosed queue grants; no app images, sign-in flows or
// telemetry collection. Required policy inputs must be approved before create.
@minLength(2)
@maxLength(12)
param namingSuffix string
param virtualNetworkName string
param containerAppsSubnetName string
param privateEndpointSubnetName string
param infrastructureResourceGroupName string
param tenantId string
param databaseAdministratorId string
param databaseAdministratorName string
param databaseAdministratorType string
param databaseBackupRetentionDays int
param vaultSoftDeleteRetentionDays int
param vaultPurgeProtection bool
param telemetryRetentionDays int
param dailyIngestionCapGb int

module network 'pilot-broker-network-spike.bicep' = {
  name: 'foundation-network'
  params: {
    virtualNetworkName: virtualNetworkName
    virtualNetworkAddressPrefix: '10.64.0.0/16'
    containerAppsSubnetName: containerAppsSubnetName
    containerAppsSubnetAddressPrefix: '10.64.0.0/23'
    privateEndpointSubnetName: privateEndpointSubnetName
    privateEndpointSubnetAddressPrefix: '10.64.2.0/24'
    staticEgressPublicIpName: 'pip-iga-pilot-dev-egress'
    natGatewayName: 'nat-iga-pilot-dev'
    serviceBusNamespaceName: 'sb-iga-pilot-dev-${namingSuffix}'
    workQueueName: 'synthetic-work'
    workloadIdentityPrefix: 'id-iga-pilot-dev'
  }
}

var zones = [
  'privatelink.azurecr.io'
  'privatelink.vaultcore.azure.net'
  // This development deployment is explicitly Azure public cloud.
  #disable-next-line no-hardcoded-env-urls
  'privatelink.blob.core.windows.net'
  'privatelink.postgres.database.azure.com'
  'privatelink.monitor.azure.com'
  'privatelink.oms.opinsights.azure.com'
  'privatelink.ods.opinsights.azure.com'
  'privatelink.agentsvc.azure-automation.net'
]
module dns 'modules/pilot-private-dns.bicep' = [
  for (zone, i) in zones: {
    name: 'foundation-dns-${i}'
    params: {
      zoneName: zone
      virtualNetworkId: resourceId('Microsoft.Network/virtualNetworks', virtualNetworkName)
      virtualNetworkLinkName: 'pilot'
    }
    dependsOn: [network]
  }
]

module registry 'modules/pilot-container-registry.bicep' = {
  name: 'foundation-registry'
  params: {
    registryName: 'iga${namingSuffix}'
    privateEndpointSubnetId: network.outputs.privateEndpointSubnetId
    registryPrivateDnsZoneId: dns[0].outputs.privateDnsZoneId
  }
}
module vault 'modules/pilot-key-vault.bicep' = {
  name: 'foundation-vault'
  params: {
    keyVaultName: 'kv-iga-${namingSuffix}'
    tenantId: tenantId
    softDeleteRetentionInDays: vaultSoftDeleteRetentionDays
    enablePurgeProtection: vaultPurgeProtection
    privateEndpointSubnetId: network.outputs.privateEndpointSubnetId
    keyVaultPrivateDnsZoneId: dns[1].outputs.privateDnsZoneId
  }
}
module database 'modules/pilot-postgresql.bicep' = {
  name: 'foundation-database'
  params: {
    serverName: 'pg-iga-${namingSuffix}'
    skuName: 'Standard_B1ms'
    skuTier: 'Burstable'
    storageSizeGB: 32
    storageAutoGrow: 'Disabled'
    backupRetentionDays: databaseBackupRetentionDays
    administratorPrincipalId: databaseAdministratorId
    administratorPrincipalName: databaseAdministratorName
    administratorPrincipalType: databaseAdministratorType
    entraTenantId: tenantId
    privateEndpointSubnetId: network.outputs.privateEndpointSubnetId
    privateDnsZoneId: dns[3].outputs.privateDnsZoneId
  }
}
module observability 'modules/pilot-observability.bicep' = {
  name: 'foundation-observability'
  params: {
    workspaceName: 'log-iga-${namingSuffix}'
    applicationInsightsName: 'ai-iga-${namingSuffix}'
    telemetryRetentionDays: telemetryRetentionDays
    dailyIngestionCapGb: dailyIngestionCapGb
  }
}
module monitorPrivateLink 'modules/pilot-monitor-private-link.bicep' = {
  name: 'foundation-monitor-private-link'
  params: {
    privateLinkScopeName: 'ampls-iga-${namingSuffix}'
    privateEndpointName: 'ampls-iga-${namingSuffix}-pe'
    workspaceName: 'log-iga-${namingSuffix}'
    applicationInsightsName: 'ai-iga-${namingSuffix}'
    virtualNetworkName: virtualNetworkName
    privateEndpointSubnetName: privateEndpointSubnetName
  }
  dependsOn: [dns, observability]
}
module containerApps 'modules/pilot-container-apps-environment.bicep' = {
  name: 'foundation-container-apps'
  params: {
    environmentName: 'cae-iga-${namingSuffix}'
    virtualNetworkName: virtualNetworkName
    containerAppsSubnetName: containerAppsSubnetName
    infrastructureResourceGroupName: infrastructureResourceGroupName
  }
  dependsOn: [network]
}

// Empty engineering container only; no evidence intake or custom data roles.
module storage 'modules/engineering-evidence-store.bicep' = {
  name: 'foundation-empty-storage'
  params: {
    storageAccountName: 'igae${namingSuffix}'
    privateEndpointSubnetId: network.outputs.privateEndpointSubnetId
    blobPrivateDnsZoneId: dns[2].outputs.privateDnsZoneId
  }
}
