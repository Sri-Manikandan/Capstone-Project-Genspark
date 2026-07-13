param name string
param location string

// Standard_B2s does NOT exist in southindia — only the v2 B-series is offered there.
// B2als_v2 is 2 vCPU / 4 GiB. See the sizing section of the design doc before changing this.
param nodeVmSize string = 'Standard_B2als_v2'

resource aks 'Microsoft.ContainerService/managedClusters@2024-09-01' = {
  name: name
  location: location
  identity: { type: 'SystemAssigned' }
  sku: {
    name: 'Base'
    tier: 'Free' // free control plane; no uptime SLA, which is fine for a demo
  }
  properties: {
    dnsPrefix: name
    enableRBAC: true

    // Both are required for workload identity, which is how pods read Key Vault
    // without any stored credential.
    oidcIssuerProfile: { enabled: true }
    securityProfile: {
      workloadIdentity: { enabled: true }
    }

    // Installs the Secrets Store CSI driver and its Azure provider.
    addonProfiles: {
      azureKeyvaultSecretsProvider: {
        enabled: true
        config: { enableSecretRotation: 'true' }
      }
    }

    agentPoolProfiles: [
      {
        name: 'system'
        mode: 'System'
        count: 1 // single node — see the design doc's sizing math
        vmSize: nodeVmSize
        osType: 'Linux'
        osSKU: 'Ubuntu'
        type: 'VirtualMachineScaleSets'
        osDiskSizeGB: 32
      }
    ]

    networkProfile: {
      networkPlugin: 'azure'
      networkPluginMode: 'overlay' // consumes far fewer IPs than classic Azure CNI
      loadBalancerSku: 'standard'
      outboundType: 'loadBalancer' // the LB also provides egress SNAT to Postgres/Stripe/Resend
    }
  }
}

output name string = aks.name
output id string = aks.id
output oidcIssuerUrl string = aks.properties.oidcIssuerProfile.issuerURL
output kubeletIdentityObjectId string = aks.properties.identityProfile.kubeletidentity.objectId
output clusterIdentityPrincipalId string = aks.identity.principalId
output nodeResourceGroup string = aks.properties.nodeResourceGroup
