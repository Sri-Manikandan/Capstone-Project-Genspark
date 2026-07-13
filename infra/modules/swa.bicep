param name string

// Static Web Apps is offered in only five regions and southindia is NOT one of them
// (verified with: az provider show -n Microsoft.Web --query
// "resourceTypes[?resourceType=='staticSites'].locations[]"). East Asia is the closest
// to India. Everything else in this deployment lives in southindia.
param location string = 'eastasia'

resource swa 'Microsoft.Web/staticSites@2023-12-01' = {
  name: name
  location: location
  sku: { name: 'Free', tier: 'Free' }
  properties: {
    // Deploys come from GitHub Actions, not from SWA's own build pipeline.
    allowConfigFileUpdates: true
  }
}

output name string = swa.name
output defaultHostname string = swa.properties.defaultHostname
