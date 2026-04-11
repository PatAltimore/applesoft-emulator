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
var planName = take('asp-${environmentName}-${resourceToken}', 40)
var webAppName = take('app-${environmentName}-${resourceToken}', 60)
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

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  kind: 'linux'
  tags: baseTags
  sku: {
    name: 'B1'
    tier: 'Basic'
    size: 'B1'
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  tags: union(baseTags, {
    'azd-service-name': 'api'
  })
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'Cors__AllowedOrigins__0'
          value: 'http://localhost:4280'
        }
        {
          name: 'Cors__AllowedOrigins__1'
          value: 'http://127.0.0.1:4280'
        }
        {
          name: 'Cors__AllowedOrigins__2'
          value: 'http://localhost:5500'
        }
        {
          name: 'Cors__AllowedOrigins__3'
          value: 'http://127.0.0.1:5500'
        }
        {
          name: 'Cors__AllowedOrigins__4'
          value: 'https://${staticWebApp.properties.defaultHostname}'
        }
      ]
    }
  }
}

output SERVICE_API_NAME string = webApp.name
output SERVICE_API_ENDPOINT_URL string = 'https://${webApp.properties.defaultHostName}'
output SERVICE_WEB_NAME string = staticWebApp.name
output SERVICE_WEB_ENDPOINT_URL string = 'https://${staticWebApp.properties.defaultHostname}'
output AZURE_LOCATION string = location
