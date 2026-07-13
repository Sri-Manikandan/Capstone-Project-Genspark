param name string
param location string

// Object ID of the user-assigned managed identity the pods run as.
param workloadIdentityPrincipalId string

resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }

    // Access policies, NOT RBAC. RBAC would be the better model, but granting it requires
    // Microsoft.Authorization/roleAssignments/write (Owner or User Access Administrator),
    // which this subscription's Contributor role does not have. Access policies are a
    // property of the vault resource itself, so Contributor can set them.
    // If the subscription is ever granted UAA, switch this to enableRbacAuthorization: true
    // and replace the policy below with a Key Vault Secrets User role assignment.
    enableRbacAuthorization: false
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: workloadIdentityPrincipalId
        permissions: {
          secrets: ['get', 'list'] // read-only: pods never write secrets
        }
      }
    ]

    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Enabled'
  }
}

output name string = kv.name
output id string = kv.id
output uri string = kv.properties.vaultUri
