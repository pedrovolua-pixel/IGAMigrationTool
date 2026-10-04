// Partial G1 spike entry point. It provisions only network/static egress and
// the Service Bus namespace, queue and isolated sender/receiver identities.
// Workloads and diagnostics come later.
@minLength(1)
param virtualNetworkName string

@minLength(1)
param virtualNetworkAddressPrefix string

@minLength(1)
param containerAppsSubnetName string

@minLength(1)
param containerAppsSubnetAddressPrefix string

@minLength(1)
param privateEndpointSubnetName string

@minLength(1)
param privateEndpointSubnetAddressPrefix string

@minLength(1)
param staticEgressPublicIpName string

@minLength(1)
param natGatewayName string

@minLength(6)
@maxLength(50)
param serviceBusNamespaceName string

@minLength(1)
param workQueueName string

@minLength(1)
@maxLength(100)
param workloadIdentityPrefix string

module network 'modules/pilot-network-egress.bicep' = {
  name: 'pilot-network-egress'
  params: {
    virtualNetworkName: virtualNetworkName
    virtualNetworkAddressPrefix: virtualNetworkAddressPrefix
    containerAppsSubnetName: containerAppsSubnetName
    containerAppsSubnetAddressPrefix: containerAppsSubnetAddressPrefix
    privateEndpointSubnetName: privateEndpointSubnetName
    privateEndpointSubnetAddressPrefix: privateEndpointSubnetAddressPrefix
    staticEgressPublicIpName: staticEgressPublicIpName
    natGatewayName: natGatewayName
  }
}

module serviceBus 'modules/pilot-service-bus-namespace.bicep' = {
  name: 'pilot-service-bus-namespace'
  params: {
    namespaceName: serviceBusNamespaceName
    staticEgressPublicIpName: staticEgressPublicIpName
  }
  dependsOn: [network]
}

module workQueue 'modules/pilot-service-bus-work-queue.bicep' = {
  name: 'pilot-service-bus-work-queue'
  params: {
    namespaceName: serviceBusNamespaceName
    workQueueName: workQueueName
  }
  dependsOn: [serviceBus]
}

module senderIdentity 'modules/pilot-workload-identity.bicep' = {
  name: 'pilot-work-sender-identity'
  params: {
    identityName: '${workloadIdentityPrefix}-sb-sender'
  }
}

module receiverIdentity 'modules/pilot-workload-identity.bicep' = {
  name: 'pilot-work-receiver-identity'
  params: {
    identityName: '${workloadIdentityPrefix}-sb-receiver'
  }
}

module workRoles 'modules/pilot-service-bus-work-roles.bicep' = {
  name: 'pilot-service-bus-work-roles'
  params: {
    namespaceName: serviceBusNamespaceName
    workQueueName: workQueueName
    senderPrincipalId: senderIdentity.outputs.principalId
    receiverPrincipalId: receiverIdentity.outputs.principalId
  }
  dependsOn: [workQueue]
}

output containerAppsSubnetId string = network.outputs.containerAppsSubnetId
output privateEndpointSubnetId string = network.outputs.privateEndpointSubnetId
output namespaceResourceId string = serviceBus.outputs.namespaceResourceId
output workQueueResourceId string = workQueue.outputs.queueResourceId
output senderIdentityResourceId string = senderIdentity.outputs.identityResourceId
output receiverIdentityResourceId string = receiverIdentity.outputs.identityResourceId
