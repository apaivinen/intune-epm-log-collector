targetScope = 'resourceGroup'

@description('Azure region for resources created by this deployment. It must match the existing Log Analytics workspace region.')
param location string

@description('Name of the existing Log Analytics workspace.')
param logAnalyticsWorkspaceName string

@description('Name of the resource group containing the existing Log Analytics workspace. Defaults to this deployment resource group.')
param logAnalyticsWorkspaceResourceGroupName string = resourceGroup().name

@description('Subscription ID containing the existing Log Analytics workspace. Defaults to this deployment subscription.')
param logAnalyticsWorkspaceSubscriptionId string = subscription().subscriptionId

@description('Name for the Linux Premium or Dedicated App Service plan.')
param appServicePlanName string

@description('App Service plan SKU name, e.g. EP1 (Elastic Premium) or P1v3 (Dedicated).')
param appServicePlanSkuName string = 'EP1'

@description('App Service plan SKU tier matching appServicePlanSkuName, e.g. ElasticPremium or PremiumV3.')
param appServicePlanSkuTier string = 'ElasticPremium'

@description('Globally unique name for the Function App.')
param functionAppName string

@description('Globally unique 3-24 character lowercase alphanumeric StorageV2 account name.')
@minLength(3)
@maxLength(24)
param storageAccountName string

@description('Name of the workspace-based Application Insights component.')
param applicationInsightsName string

@description('Name of the Data Collection Rule.')
param dataCollectionRuleName string = 'dcr-epm-log-collector'

@description('Function timer schedule in NCRONTAB format.')
param epmCollectionSchedule string = '0 */5 * * * *'

@description('Microsoft Graph beta API base URL.')
param graphBaseUrl string = 'https://graph.microsoft.com/beta'

@description('Maximum JSON payload size per Logs Ingestion request in bytes.')
@minValue(3)
@maxValue(1000000)
param logsIngestionMaxBatchSizeBytes int = 900000

@description('Blob container used to store collection checkpoints.')
param checkpointContainerName string = 'checkpoints'

@description('Blob name used to store the collection watermark.')
param checkpointBlobName string = 'epm-elevation-requests.json'

@description('Collection overlap window used when filtering elevation requests.')
param collectionOverlap string = '00:05:00'

@description('Storage account redundancy SKU.')
@allowed([
  'Standard_LRS'
  'Standard_GRS'
  'Standard_RAGRS'
  'Standard_ZRS'
  'Standard_GZRS'
  'Standard_RAGZRS'
])
param storageSkuName string = 'Standard_LRS'

@description('Tags to apply to created resources.')
param tags object = {}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' existing = {
  name: logAnalyticsWorkspaceName
  scope: resourceGroup(logAnalyticsWorkspaceSubscriptionId, logAnalyticsWorkspaceResourceGroupName)
}

var functionRuntimeSettings = {
  EpmCollectionSchedule: epmCollectionSchedule
  GraphBaseUrl: graphBaseUrl
  LogsIngestionEndpoint: dataCollectionRuleModule.outputs.logsIngestionEndpoint
  DataCollectionRuleImmutableId: dataCollectionRuleModule.outputs.dataCollectionRuleImmutableId
  DataCollectionStreamName: dataCollectionRuleModule.outputs.inputStreamName
  LogsIngestionMaxBatchSizeBytes: string(logsIngestionMaxBatchSizeBytes)
  CheckpointContainerName: checkpointContainerName
  CheckpointBlobName: checkpointBlobName
  CollectionOverlap: collectionOverlap
}

module storageModule 'modules/storage-account.bicep' = {
  params: {
    storageAccountName: storageAccountName
    location: location
    skuName: storageSkuName
    checkpointContainerName: checkpointContainerName
    tags: tags
  }
}

module appServicePlanModule 'modules/app-service-plan.bicep' = {
  params: {
    appServicePlanName: appServicePlanName
    location: location
    skuName: appServicePlanSkuName
    skuTier: appServicePlanSkuTier
    tags: tags
  }
}

module applicationInsightsModule 'modules/application-insights.bicep' = {
  params: {
    componentName: applicationInsightsName
    location: location
    workspaceResourceId: logAnalyticsWorkspace.id
    tags: tags
  }
}

module logAnalyticsTableModule 'modules/log-analytics-table.bicep' = {
  scope: resourceGroup(logAnalyticsWorkspaceSubscriptionId, logAnalyticsWorkspaceResourceGroupName)
  params: {
    workspaceName: logAnalyticsWorkspaceName
  }
}

module dataCollectionRuleModule 'modules/data-collection-rule.bicep' = {
  params: {
    ruleName: dataCollectionRuleName
    location: location
    workspaceResourceId: logAnalyticsWorkspace.id
  }
  dependsOn: [
    logAnalyticsTableModule
  ]
}

module functionAppModule 'modules/function-app.bicep' = {
  params: {
    functionAppName: functionAppName
    location: location
    appServicePlanName: appServicePlanName
    storageAccountName: storageAccountName
    applicationInsightsComponentName: applicationInsightsName
    runtimeSettings: functionRuntimeSettings
  }
  dependsOn: [
    storageModule
    applicationInsightsModule
    appServicePlanModule
  ]
}

module roleAssignmentsModule 'modules/role-assignments.bicep' = {
  params: {
    dataCollectionRuleName: dataCollectionRuleName
    functionAppPrincipalId: functionAppModule.outputs.functionAppPrincipalId
  }
}

output functionAppId string = functionAppModule.outputs.functionAppId
output functionAppName string = functionAppModule.outputs.functionAppName
output functionAppHostName string = functionAppModule.outputs.functionAppHostName
output functionAppPrincipalId string = functionAppModule.outputs.functionAppPrincipalId
output storageAccountId string = storageModule.outputs.storageAccountId
output applicationInsightsComponentId string = applicationInsightsModule.outputs.componentId
output logAnalyticsTableId string = logAnalyticsTableModule.outputs.tableId
output dataCollectionRuleId string = dataCollectionRuleModule.outputs.dataCollectionRuleId
output dataCollectionRuleImmutableId string = dataCollectionRuleModule.outputs.dataCollectionRuleImmutableId
output logsIngestionEndpoint string = dataCollectionRuleModule.outputs.logsIngestionEndpoint
output dataCollectionRuleRoleAssignmentId string = roleAssignmentsModule.outputs.roleAssignmentId
