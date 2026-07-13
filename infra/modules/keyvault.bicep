param name string
param location string

// Object ID of the user-assigned managed identity the pods run as.
param workloadIdentityPrincipalId string

// Object ID of the human/principal running the deployment. Required: with RBAC disabled,
// subscription Contributor grants NO data-plane access to Key Vault, so without an explicit
// policy the deployer cannot even write the secrets ("does not have secrets set permission").
param deployerObjectId string

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
        // The pods. Read-only: they never write secrets.
        tenantId: subscription().tenantId
        objectId: workloadIdentityPrincipalId
        permissions: {
          secrets: ['get', 'list']
        }
      }
      {
        // The deployer, so provision.sh can populate the vault.
        tenantId: subscription().tenantId
        objectId: deployerObjectId
        permissions: {
          secrets: ['get', 'list', 'set', 'delete']
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
