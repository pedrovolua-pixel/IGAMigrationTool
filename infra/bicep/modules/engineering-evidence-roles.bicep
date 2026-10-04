// Pilot draft. Deploy only after the store module creates the container and
// the three dedicated managed identities exist in the approved subscription.
@minLength(3)
@maxLength(24)
param storageAccountName string

@description('Principal ID of the dedicated evidence intake managed identity.')
param intakePrincipalId string

@description('Principal ID of the dedicated evidence verification managed identity.')
param verificationPrincipalId string

@description('Principal ID of the dedicated evidence purge managed identity.')
param purgePrincipalId string

var blobActionPrefix = 'Microsoft.Storage/storageAccounts/blobServices/containers/blobs/'

resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2025-06-01' existing = {
  name: '${storageAccountName}/default/gate-evidence'
}

resource intakeRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(resourceGroup().id, storageAccountName, 'gate-evidence-intake')
  properties: {
    roleName: '${storageAccountName} gate evidence intake'
    description: 'Create new gate evidence blobs only.'
    type: 'CustomRole'
    assignableScopes: [
      resourceGroup().id
    ]
    permissions: [
      {
        actions: []
        notActions: []
        dataActions: [
          '${blobActionPrefix}add/action'
        ]
        notDataActions: []
      }
    ]
  }
}

resource verificationRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(resourceGroup().id, storageAccountName, 'gate-evidence-verification')
  properties: {
    roleName: '${storageAccountName} gate evidence verification'
    description: 'Read gate evidence blobs for authorized verification only.'
    type: 'CustomRole'
    assignableScopes: [
      resourceGroup().id
    ]
    permissions: [
      {
        actions: []
        notActions: []
        dataActions: [
          '${blobActionPrefix}read'
        ]
        notDataActions: []
      }
    ]
  }
}

resource purgeRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(resourceGroup().id, storageAccountName, 'gate-evidence-purge')
  properties: {
    roleName: '${storageAccountName} gate evidence purge'
    description: 'Read and remove current and previous gate evidence blob versions.'
    type: 'CustomRole'
    assignableScopes: [
      resourceGroup().id
    ]
    permissions: [
      {
        actions: []
        notActions: []
        dataActions: [
          '${blobActionPrefix}read'
          '${blobActionPrefix}delete'
          '${blobActionPrefix}deleteBlobVersion/action'
        ]
        notDataActions: []
      }
    ]
  }
}

resource intakeAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(container.id, intakePrincipalId, intakeRole.id)
  scope: container
  properties: {
    roleDefinitionId: intakeRole.id
    principalId: intakePrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource verificationAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(container.id, verificationPrincipalId, verificationRole.id)
  scope: container
  properties: {
    roleDefinitionId: verificationRole.id
    principalId: verificationPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource purgeAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(container.id, purgePrincipalId, purgeRole.id)
  scope: container
  properties: {
    roleDefinitionId: purgeRole.id
    principalId: purgePrincipalId
    principalType: 'ServicePrincipal'
  }
}
