// ADR-0011 local preparation only. This is NOT a runnable portal or BFF.
// The only prospective resource module is permanently disabled in this packet.
@allowed([false])
param deployEnvironment bool
@minLength(1)
param environmentName string
@minLength(1)
param virtualNetworkName string
@minLength(1)
param containerAppsSubnetName string
@minLength(1)
param natGatewayName string
@minLength(1)
param infrastructureResourceGroupName string

module prospectiveEnvironment 'modules/pilot-https-container-apps-environment.bicep' = if (deployEnvironment) {
  name: 'disabled-https-environment-preparation'
  params: {
    environmentName: environmentName
    virtualNetworkName: virtualNetworkName
    containerAppsSubnetName: containerAppsSubnetName
    natGatewayName: natGatewayName
    infrastructureResourceGroupName: infrastructureResourceGroupName
  }
}

// Intentionally no app, image, identity, ingress, provider/SQL/key bindings or output.
// Separate approved production composition must bind actual generated origin and
// immediate proxy evidence before any deployment/admission/public activation.
