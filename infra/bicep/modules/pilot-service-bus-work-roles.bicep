// Partial Milestone 1 module. The queue and managed identities must exist and
// be verified before any role assignment is deployed.
@minLength(6)
@maxLength(50)
param namespaceName string

@minLength(1)
param workQueueName string

@minLength(1)
param senderPrincipalId string

@minLength(1)
param receiverPrincipalId string

var senderRoleId = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
var receiverRoleId = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'

resource serviceBus 'Microsoft.ServiceBus/namespaces@2026-01-01' existing = {
  name: namespaceName
}

resource workQueue 'Microsoft.ServiceBus/namespaces/queues@2026-01-01' existing = {
  parent: serviceBus
  name: workQueueName
}

resource senderRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(workQueue.id, senderPrincipalId, senderRoleId)
  scope: workQueue
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', senderRoleId)
    principalId: senderPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource receiverRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(workQueue.id, receiverPrincipalId, receiverRoleId)
  scope: workQueue
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', receiverRoleId)
    principalId: receiverPrincipalId
    principalType: 'ServicePrincipal'
  }
}
