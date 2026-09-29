targetScope = 'resourceGroup'

@description('Name of the Data Collection Rule.')
param ruleName string = 'dcr-epm-log-collector'

@description('Azure region for the Data Collection Rule.')
param location string

@description('Resource ID of the destination Log Analytics workspace.')
param workspaceResourceId string

var inputStreamName = 'Custom-EpmElevationRequests'
var outputStreamName = 'Custom-EpmElevationRequests_CL'
var logAnalyticsDestinationName = 'logAnalyticsDestination'

resource dataCollectionRule 'Microsoft.Insights/dataCollectionRules@2023-03-11' = {
  name: ruleName
  location: location
  properties: {
    description: 'Routes Intune Endpoint Privilege Management elevation requests to Log Analytics.'
    destinations: {
      logAnalytics: [
        {
          name: logAnalyticsDestinationName
          workspaceResourceId: workspaceResourceId
        }
      ]
    }
    dataFlows: [
      {
        streams: [inputStreamName]
        destinations: [logAnalyticsDestinationName]
        transformKql: 'source'
        outputStream: outputStreamName
      }
    ]
    streamDeclarations: {
      'Custom-EpmElevationRequests': {
        columns: [
          {
            name: 'TimeGenerated'
            type: 'datetime'
          }
          {
            name: 'ElevationRequestId'
            type: 'string'
          }
          {
            name: 'RequestCreatedDateTime'
            type: 'datetime'
          }
          {
            name: 'RequestLastModifiedDateTime'
            type: 'datetime'
          }
          {
            name: 'Status'
            type: 'string'
          }
          {
            name: 'RequestedByUserId'
            type: 'string'
          }
          {
            name: 'RequestedByUserPrincipalName'
            type: 'string'
          }
          {
            name: 'RequestedOnDeviceId'
            type: 'string'
          }
          {
            name: 'DeviceName'
            type: 'string'
          }
          {
            name: 'RequestJustification'
            type: 'string'
          }
          {
            name: 'FileName'
            type: 'string'
          }
          {
            name: 'FilePath'
            type: 'string'
          }
          {
            name: 'FileDescription'
            type: 'string'
          }
          {
            name: 'FileHash'
            type: 'string'
          }
          {
            name: 'PublisherName'
            type: 'string'
          }
          {
            name: 'ProductName'
            type: 'string'
          }
          {
            name: 'ProductInternalName'
            type: 'string'
          }
          {
            name: 'ProductVersion'
            type: 'string'
          }
          {
            name: 'RequestExpiryDateTime'
            type: 'datetime'
          }
          {
            name: 'ReviewCompletedByUserId'
            type: 'string'
          }
          {
            name: 'ReviewCompletedByUserPrincipalName'
            type: 'string'
          }
          {
            name: 'ReviewCompletedDateTime'
            type: 'datetime'
          }
          {
            name: 'ReviewerJustification'
            type: 'string'
          }
          {
            name: 'IngestionTime'
            type: 'datetime'
          }
          {
            name: 'Source'
            type: 'string'
          }
        ]
      }
    }
  }
}

output dataCollectionRuleId string = dataCollectionRule.id
output dataCollectionRuleImmutableId string = dataCollectionRule.properties.immutableId
output inputStreamName string = inputStreamName
output outputStreamName string = outputStreamName
