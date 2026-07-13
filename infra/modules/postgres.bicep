param name string
param location string
param administratorLogin string = 'emsadmin'

@secure()
param administratorPassword string

resource pg 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: name
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorPassword
    storage: { storageSizeGB: 32 }
    backup: {
      backupRetentionDays: 7 // point-in-time restore; the reason we did not run Postgres in-cluster
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: { mode: 'Disabled' }
    network: { publicNetworkAccess: 'Enabled' }
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: pg
  name: 'eventmanagement'
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// NOTE: the firewall rule pinning access to the AKS egress IP is deliberately NOT here.
// That IP does not exist until AKS has created its load balancer, so infra.yml adds the
// rule with `az` after this deployment completes.

output fqdn string = pg.properties.fullyQualifiedDomainName
output name string = pg.name
