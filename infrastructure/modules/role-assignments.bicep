targetScope = 'resourceGroup'

@description('Name of the existing Data Collection Rule to which the runtime application sends logs.')
param dataCollectionRuleName string

@description('Object ID of the runtime service principal. This is not the application/client ID.')
param runtimeServicePrincipalObjectId string

var monitoringMetricsPublisherRoleDefinitionGuid = '3913510d-42f4-4e42-8a64-420c390055eb'

resource dataCollectionRule 'Microsoft.Insights/dataCollectionRules@2024-03-11' existing = {
  name: dataCollectionRuleName
}

resource runtimeDcrRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(
    dataCollectionRule.id,
    runtimeServicePrincipalObjectId,
    monitoringMetricsPublisherRoleDefinitionGuid)
  scope: dataCollectionRule
  properties: {
    principalId: runtimeServicePrincipalObjectId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      monitoringMetricsPublisherRoleDefinitionGuid)
    description: 'Allows the EPM collector to send logs to its Data Collection Rule.'
  }
}

output roleAssignmentId string = runtimeDcrRoleAssignment.id
output roleDefinitionId string = runtimeDcrRoleAssignment.properties.roleDefinitionId
