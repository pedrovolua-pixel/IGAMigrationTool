// Partial Milestone 1 queue. PostgreSQL remains the work-state source of truth;
// this queue carries opaque delivery signals only.
@minLength(6)
@maxLength(50)
param namespaceName string

@minLength(1)
param workQueueName string

resource serviceBus 'Microsoft.ServiceBus/namespaces@2026-01-01' existing = {
  name: namespaceName
}

resource workQueue 'Microsoft.ServiceBus/namespaces/queues@2026-01-01' = {
  parent: serviceBus
  name: workQueueName
  properties: {
    deadLetteringOnMessageExpiration: true
    requiresDuplicateDetection: true
  }
}

output queueResourceId string = workQueue.id
