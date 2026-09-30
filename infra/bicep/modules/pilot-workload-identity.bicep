// Instantiate separately for every workload class. This module grants no roles.
@minLength(1)
param identityName string

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: identityName
  location: 'eastus2'
}

output identityResourceId string = identity.id
output principalId string = identity.properties.principalId
output clientId string = identity.properties.clientId
