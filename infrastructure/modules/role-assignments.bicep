targetScope = 'resourceGroup'

@description('Name of the existing Data Collection Rule to which the runtime application sends logs.')
param dataCollectionRuleName string

@description('Principal ID of the Function App system-assigned managed identity.')
param functionAppPrincipalId string

var monitoringMetricsPublisherRoleDefinitionGuid = '3913510d-42f4-4e42-8a64-420c390055eb'

resource dataCollectionRule 'Microsoft.Insights/dataCollectionRules@2024-03-11' existing = {
  name: dataCollectionRuleName
}

resource runtimeDcrRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(
    dataCollectionRule.id,
    functionAppPrincipalId,
    monitoringMetricsPublisherRoleDefinitionGuid)
  scope: dataCollectionRule
  properties: {
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      monitoringMetricsPublisherRoleDefinitionGuid)
    description: 'Allows the EPM collector to send logs to its Data Collection Rule.'
  }
}

output roleAssignmentId string = runtimeDcrRoleAssignment.id
output roleDefinitionId string = runtimeDcrRoleAssignment.properties.roleDefinitionId
