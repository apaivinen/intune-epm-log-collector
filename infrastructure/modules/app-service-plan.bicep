targetScope = 'resourceGroup'

@description('Name for the Linux App Service plan.')
param appServicePlanName string

@description('Azure region for the App Service plan.')
param location string

@description('App Service plan SKU name, e.g. EP1 (Elastic Premium) or P1v3 (Dedicated).')
param skuName string = 'EP1'

@description('App Service plan SKU tier matching skuName, e.g. ElasticPremium or PremiumV3.')
param skuTier string = 'ElasticPremium'

@description('Tags to apply to the App Service plan.')
param tags object = {}

resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: skuName
    tier: skuTier
  }
  properties: {
    reserved: true
  }
}

output appServicePlanId string = appServicePlan.id
output appServicePlanName string = appServicePlan.name
