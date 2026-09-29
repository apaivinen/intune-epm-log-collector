using '../main.bicep'

// Replace the example resource names and IDs before deployment.
param location = 'westeurope'
param logAnalyticsWorkspaceName = 'law-epm-example'
param appServicePlanName = 'asp-epm-linux-example'
param functionAppName = 'func-epm-example-001'
param storageAccountName = 'stepmexample00001'
param applicationInsightsName = 'appi-epm-example'
param dataCollectionRuleName = 'dcr-epm-log-collector'

// Use the service principal object ID here, not GraphClientId.
param runtimeServicePrincipalObjectId = '00000000-0000-0000-0000-000000000000'
param graphTenantId = '00000000-0000-0000-0000-000000000000'
param graphClientId = '00000000-0000-0000-0000-000000000000'

// Set EPM_GRAPH_CLIENT_SECRET before compiling/deploying. The fallback is a placeholder, not a usable credential.
param graphClientSecret = readEnvironmentVariable('EPM_GRAPH_CLIENT_SECRET', 'REPLACE_WITH_EPM_GRAPH_CLIENT_SECRET')

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
