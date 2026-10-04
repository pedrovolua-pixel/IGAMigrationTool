// Payload-free monitoring foundation, not an audit/evidence system of record.
// No SDK, diagnostic export, data grant, or availability test is enabled here.
// https://learn.microsoft.com/azure/templates/microsoft.operationalinsights/2025-07-01/workspaces
// https://learn.microsoft.com/azure/templates/microsoft.insights/2020-02-02/components
@minLength(4)
@maxLength(63)
param workspaceName string

@minLength(1)
param applicationInsightsName string

@description('Requires explicit operational telemetry retention approval. Audit/evidence 12-month policy does not supply this input. Application Insights tables can retain 90 days independently until table policy is configured; collection must stay disabled meanwhile.')
@allowed([30, 60, 90, 120, 180, 270, 365, 550, 730])
param telemetryRetentionDays int

@description('Explicit ingestion daily cap for the cost proposal, in GB. Azure caps are delayed and are not a hard spending limit.')
@minValue(1)
param dailyIngestionCapGb int

resource workspace 'Microsoft.OperationalInsights/workspaces@2025-07-01' = {
  name: workspaceName
  location: 'eastus2'
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: telemetryRetentionDays
    workspaceCapping: {
      dailyQuotaGb: dailyIngestionCapGb
    }
    features: {
      disableLocalAuth: true
      enableDataExport: false
      // Operations readers require explicit workspace permission.
      enableLogAccessUsingOnlyResourcePermissions: false
    }
    publicNetworkAccessForIngestion: 'Disabled'
    publicNetworkAccessForQuery: 'Disabled'
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: 'eastus2'
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    IngestionMode: 'LogAnalytics'
    DisableLocalAuth: true
    DisableIpMasking: false
    publicNetworkAccessForIngestion: 'Disabled'
    publicNetworkAccessForQuery: 'Disabled'
  }
}

output workspaceResourceId string = workspace.id
output applicationInsightsResourceId string = applicationInsights.id
