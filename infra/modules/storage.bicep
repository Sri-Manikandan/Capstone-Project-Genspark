param name string
param location string

// StorageV2 with anonymous blob read enabled at the account level. Event banners are public
// content, so the container below allows anonymous blob (not container-listing) access, giving
// plain, cacheable image URLs with no SAS. No role assignments are made — the pods authenticate
// with the account key from Key Vault, matching the rest of this deployment.
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: true
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource eventImages 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobServices
  name: 'event-images'
  properties: {
    publicAccess: 'Blob'
  }
}

output name string = storage.name
output blobEndpoint string = storage.properties.primaryEndpoints.blob
