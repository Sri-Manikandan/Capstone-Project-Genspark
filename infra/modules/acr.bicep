param name string
param location string

resource acr 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: name
  location: location
  sku: { name: 'Basic' }
  properties: {
    // The better model is adminUserEnabled: false, with AKS's kubelet identity granted the
    // AcrPull role. That needs Microsoft.Authorization/roleAssignments/write, which this
    // subscription's Contributor role does not have. So instead the admin user is enabled
    // and Kubernetes pulls with an imagePullSecret built from these credentials.
    // Trade-off: a long-lived shared password instead of a scoped managed identity.
    adminUserEnabled: true
  }
}

output loginServer string = acr.properties.loginServer
output id string = acr.id
output name string = acr.name
