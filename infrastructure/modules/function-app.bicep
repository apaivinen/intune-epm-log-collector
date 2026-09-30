targetScope = 'resourceGroup'

@description('Globally unique name for the Function App.')
param functionAppName string

@description('Azure region for the Function App.')
param location string

@description('Name of the Linux Premium or Dedicated App Service plan in this resource group. Linux Consumption does not support .NET 10 Functions.')
param appServicePlanName string

@description('Name of an existing storage account in this resource group used by the Functions host and checkpoint service.')
param storageAccountName string

@description('Name of the existing workspace-based Application Insights component.')
param applicationInsightsComponentName string

@secure()
@description('Function runtime settings, including Logs Ingestion configuration.')
param runtimeSettings object

resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' existing = {
  name: appServicePlanName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: applicationInsightsComponentName
}

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
var parameterAppSettings = [for setting in items(runtimeSettings): {
  name: setting.key
  value: string(setting.value)
}]
var platformAppSettings = [
  {
    name: 'AzureWebJobsStorage'
    value: storageConnectionString
  }
  {
    name: 'FUNCTIONS_EXTENSION_VERSION'
    value: '~4'
  }
  {
    name: 'FUNCTIONS_WORKER_RUNTIME'
    value: 'dotnet-isolated'
  }
  {
    name: 'WEBSITE_RUN_FROM_PACKAGE'
    value: '1'
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: applicationInsights.properties.ConnectionString
  }
]

// Runtime values are parameterized securely; infrastructure-owned settings are appended separately.

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      alwaysOn: true
      ftpsState: 'Disabled'
      linuxFxVersion: 'DOTNET-ISOLATED|10.0'
      minTlsVersion: '1.2'
      appSettings: concat(parameterAppSettings, platformAppSettings)
    }
  }
}

output functionAppId string = functionApp.id
output functionAppName string = functionApp.name
output functionAppHostName string = functionApp.properties.defaultHostName
output functionAppPrincipalId string = functionApp.identity.principalId
