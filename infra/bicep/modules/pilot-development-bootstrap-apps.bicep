// Development bootstrap only: inert probe host and worker, never the product BFF.
// Deployment requires a separately approved priced window and removal of both apps.
// https://learn.microsoft.com/azure/templates/microsoft.app/2026-01-01/containerapps
// https://learn.microsoft.com/azure/container-apps/managed-identity-image-pull
// https://learn.microsoft.com/azure/azure-resource-manager/bicep/bicep-functions-flow-control
@minLength(1)
param environmentName string
@minLength(5)
@maxLength(50)
param registryName string
@minLength(1)
param webIdentityName string
@minLength(1)
param workerIdentityName string
@minLength(2)
@maxLength(32)
param webAppName string
@minLength(2)
@maxLength(32)
param workerAppName string

// This deliberately fixed repository is populated only by the reviewed package pipeline.
@allowed(['iga/azure-development-bootstrap'])
param imageRepository string
@description('Actual published image manifest SHA256: 64 lowercase hexadecimal characters, without sha256: prefix. Zero placeholder is rejected.')
@minLength(64)
@maxLength(64)
param imageDigest string

resource environment 'Microsoft.App/managedEnvironments@2026-01-01' existing = {
  name: environmentName
}
resource registry 'Microsoft.ContainerRegistry/registries@2025-11-01' existing = {
  name: registryName
}
resource webIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' existing = {
  name: webIdentityName
}
resource workerIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' existing = {
  name: workerIdentityName
}

var hexAlphabet = '0123456789abcdef'
var digestValid = (length(imageDigest) == 64 && imageDigest != '0000000000000000000000000000000000000000000000000000000000000000' && length(filter(
  range(0, length(imageDigest)),
  i => !contains(hexAlphabet, substring(imageDigest, i, 1))
)) == 0)
var identityNamesDistinct = toLower(webIdentityName) != toLower(workerIdentityName)
var appNamesDistinct = toLower(webAppName) != toLower(workerAppName)
var validatedDigest = digestValid && identityNamesDistinct && appNamesDistinct
  ? imageDigest
  : fail('Require a real lowercase SHA256 image digest, distinct workload identities and distinct application names.')
var environmentValid = (toLower(environment.location) == 'eastus2' && environment.properties.publicNetworkAccess == 'Disabled' && environment.properties.vnetConfiguration.internal == true && length(filter(
  environment.properties.workloadProfiles,
  profile => profile.name == 'Consumption' && profile.workloadProfileType == 'Consumption'
)) == 1)
var environmentId = environmentValid
  ? environment.id
  : fail('Require the existing private internal East US 2 Consumption environment.')
var registryValid = (toLower(registry.location) == 'eastus2' && registry.sku.name == 'Premium' && registry.properties.publicNetworkAccess == 'Disabled' && registry.properties.adminUserEnabled == false && registry.properties.anonymousPullEnabled == false && registry.properties.networkRuleBypassOptions == 'None')
var registryServer = registryValid
  ? registry.properties.loginServer
  : fail('Require the existing private Premium registry with local and anonymous authentication disabled and no network bypass.')
var image = '${registryServer}/${imageRepository}@sha256:${validatedDigest}'

resource web 'Microsoft.App/containerApps@2026-01-01' = {
  name: webAppName
  location: 'eastus2'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${webIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      registries: [{ server: registryServer, identity: webIdentity.id }]
      ingress: {
        external: false
        allowInsecure: false
        targetPort: 8080
        transport: 'http'
      }
    }
    template: {
      containers: [
        {
          name: 'bootstrap-web'
          image: image
          command: ['dotnet', 'AzureDevelopmentBootstrap.dll']
          args: ['--azure-development-bootstrap', '--role', 'web']
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/health/live', port: 8080, scheme: 'HTTP' }
              initialDelaySeconds: 5
              periodSeconds: 10
              timeoutSeconds: 2
              failureThreshold: 3
            }
          ]
          resources: { cpu: any('0.25'), memory: '0.5Gi' }
        }
      ]
      // One replica per role only during the approved bounded test window.
      scale: { minReplicas: 1, maxReplicas: 1, rules: [] }
    }
  }
}

resource worker 'Microsoft.App/containerApps@2026-01-01' = {
  name: workerAppName
  location: 'eastus2'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${workerIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      registries: [{ server: registryServer, identity: workerIdentity.id }]
    }
    template: {
      containers: [
        {
          name: 'bootstrap-worker'
          image: image
          command: ['dotnet', 'AzureDevelopmentBootstrap.dll']
          args: ['--azure-development-bootstrap', '--role', 'worker']
          probes: []
          resources: { cpu: any('0.25'), memory: '0.5Gi' }
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 1, rules: [] }
    }
  }
}
// No grants, secret/env configuration, diagnostic exports, federation or outputs.
// Preflight must separately prove private DNS, effective image-pull grants and
// registry ARM audience tokens; existing resource checks do not prove live access.
