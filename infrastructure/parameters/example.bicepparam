using '../main.bicep'

// Replace the example resource names and IDs before deployment.
param location = 'westeurope'
param logAnalyticsWorkspaceName = 'law-epm-example'
param logAnalyticsWorkspaceResourceGroupName = 'rg-epm-example-sec-mon'
param appServicePlanName = 'asp-epm-linux-example'
param appServicePlanSkuName = 'EP1'
param appServicePlanSkuTier = 'ElasticPremium'
param functionAppName = 'func-epm-example-001'
param storageAccountName = 'stepmexample00001'
param applicationInsightsName = 'appi-epm-example'
param dataCollectionRuleName = 'dcr-epm-log-collector'

param epmCollectionSchedule = '0 */5 * * * *'
param graphBaseUrl = 'https://graph.microsoft.com/beta'
param logsIngestionMaxBatchSizeBytes = 900000
param checkpointContainerName = 'checkpoints'
param checkpointBlobName = 'epm-elevation-requests.json'
param collectionOverlap = '00:05:00'
param storageSkuName = 'Standard_LRS'
param tags = {
  application: 'intune-epm-log-collector'
  environment: 'example'
}
