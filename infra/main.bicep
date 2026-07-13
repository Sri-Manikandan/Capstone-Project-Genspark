// EMS production infrastructure.
//
// PERMISSIONS NOTE: this template deliberately creates NO role assignments, because the
// subscription's Contributor role cannot perform Microsoft.Authorization/roleAssignments/write.
// Three things are done differently as a result:
//   1. Key Vault uses access policies instead of RBAC        (see modules/keyvault.bicep)
//   2. ACR enables its admin user instead of an AcrPull grant (see modules/acr.bicep)
//   3. The ingress public IP is NOT pre-created here — AKS allocates one in its own node
//      resource group, which it already owns, and infra.yml then attaches a DNS label.
// If the subscription is ever granted User Access Administrator, all three should be
// reverted to the RBAC/managed-identity model. See the design doc.

targetScope = 'resourceGroup'

param location string = 'southindia'
param swaLocation string = 'eastasia' // Static Web Apps is not offered in southindia
param prefix string = 'ems'

@secure()
param postgresAdminPassword string

// Keeps globally-unique names (ACR, Key Vault) collision-free.
var suffix = uniqueString(resourceGroup().id)
var shortSuffix = take(suffix, 8)

var acrName = '${prefix}acr${suffix}' // ACR names must be alphanumeric only
var kvName = '${prefix}-kv-${shortSuffix}' // Key Vault names max 24 chars
var aksName = '${prefix}-aks'

// ── Workload identity: the pods' Azure identity for reading Key Vault ─────────────────
// A user-assigned managed identity plus a federated credential is NOT an Entra app
// registration, so Contributor can create both. This is why workload identity still works
// even though GitHub Actions OIDC does not.
resource workloadIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-workload-id'
  location: location
}

module aks 'modules/aks.bicep' = {
  name: 'aks'
  params: {
    name: aksName
    location: location
  }
}

// Federates the Kubernetes ServiceAccount ems:ems-sa to the managed identity, so pods
// exchange their projected SA token for an Azure token with no stored credential.
// The subject MUST exactly match the namespace and ServiceAccount name in k8s/.
resource federatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: workloadIdentity
  name: 'ems-service-account'
  properties: {
    issuer: aks.outputs.oidcIssuerUrl
    subject: 'system:serviceaccount:ems:ems-sa'
    audiences: ['api://AzureADTokenExchange']
  }
}

module acr 'modules/acr.bicep' = {
  name: 'acr'
  params: {
    name: acrName
    location: location
  }
}

module keyvault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    name: kvName
    location: location
    workloadIdentityPrincipalId: workloadIdentity.properties.principalId
  }
}

module postgres 'modules/postgres.bicep' = {
  name: 'postgres'
  params: {
    name: '${prefix}-pg-${suffix}'
    location: location
    administratorPassword: postgresAdminPassword
  }
}

module swa 'modules/swa.bicep' = {
  name: 'swa'
  params: {
    name: '${prefix}-web-${shortSuffix}'
    location: swaLocation
  }
}

output acrLoginServer string = acr.outputs.loginServer
output acrName string = acr.outputs.name
output aksName string = aks.outputs.name
output aksNodeResourceGroup string = aks.outputs.nodeResourceGroup
output keyVaultName string = keyvault.outputs.name
output postgresFqdn string = postgres.outputs.fqdn
output postgresName string = postgres.outputs.name
output workloadIdentityClientId string = workloadIdentity.properties.clientId
output swaName string = swa.outputs.name
output resourceGroupName string = resourceGroup().name
