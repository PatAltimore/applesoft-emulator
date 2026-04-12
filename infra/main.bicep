targetScope = 'resourceGroup'

@description('Name of the azd environment used to make resource names unique.')
param environmentName string

@description('Azure region for all provisioned resources.')
param location string = resourceGroup().location

@description('Azure region for the Static Web App frontend.')
param staticWebAppLocation string = 'westus2'

@description('Tags applied to all resources.')
param tags object = {}

var resourceToken = toLower(uniqueString(subscription().subscriptionId, resourceGroup().id, environmentName))
var staticWebAppName = take('swa-${environmentName}-${resourceToken}', 40)
var baseTags = union(tags, {
  'azd-env-name': environmentName
})

resource staticWebApp 'Microsoft.Web/staticSites@2025-03-01' = {
  name: staticWebAppName
  location: staticWebAppLocation
  tags: union(baseTags, {
    'azd-service-name': 'web'
  })
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    allowConfigFileUpdates: true
    publicNetworkAccess: 'Enabled'
    stagingEnvironmentPolicy: 'Disabled'
  }
}

output SERVICE_WEB_NAME string = staticWebApp.name
output SERVICE_WEB_ENDPOINT_URL string = 'https://${staticWebApp.properties.defaultHostname}'
output AZURE_LOCATION string = location
