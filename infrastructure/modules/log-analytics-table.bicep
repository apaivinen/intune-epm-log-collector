targetScope = 'resourceGroup'

@description('Name of the existing Log Analytics workspace that will contain the custom table.')
param workspaceName string

@description('Name of the custom elevation request table.')
param tableName string = 'EpmElevationRequests_CL'

resource workspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' existing = {
  name: workspaceName
}

resource elevationRequestsTable 'Microsoft.OperationalInsights/workspaces/tables@2022-10-01' = {
  parent: workspace
  name: tableName
  properties: {
    plan: 'Analytics'
    schema: {
      name: tableName
      description: 'Intune Endpoint Privilege Management elevation requests collected from Microsoft Graph.'
      columns: [
        {
          name: 'TimeGenerated'
          type: 'dateTime'
          description: 'Timestamp used by Log Analytics for the record.'
        }
        {
          name: 'ElevationRequestId'
          type: 'string'
          description: 'Microsoft Graph elevation request identifier.'
        }
        {
          name: 'RequestCreatedDateTime'
          type: 'dateTime'
          description: 'Time the elevation request was created.'
        }
        {
          name: 'RequestLastModifiedDateTime'
          type: 'dateTime'
          description: 'Time the elevation request was last modified.'
        }
        {
          name: 'Status'
          type: 'string'
          description: 'Current elevation request status.'
        }
        {
          name: 'RequestedByUserId'
          type: 'string'
          description: 'Entra object ID of the requesting user.'
        }
        {
          name: 'RequestedByUserPrincipalName'
          type: 'string'
          description: 'User principal name of the requesting user.'
        }
        {
          name: 'RequestedOnDeviceId'
          type: 'string'
          description: 'Intune device identifier for the device used to submit the request.'
        }
        {
          name: 'DeviceName'
          type: 'string'
          description: 'Name of the device used to submit the request.'
        }
        {
          name: 'RequestJustification'
          type: 'string'
          description: 'Justification supplied by the requesting user.'
        }
        {
          name: 'FileName'
          type: 'string'
          description: 'Name of the application file requested for elevation.'
        }
        {
          name: 'FilePath'
          type: 'string'
          description: 'Path of the application file requested for elevation.'
        }
        {
          name: 'FileDescription'
          type: 'string'
          description: 'Description of the application file.'
        }
        {
          name: 'FileHash'
          type: 'string'
          description: 'Hash of the application file.'
        }
        {
          name: 'PublisherName'
          type: 'string'
          description: 'Publisher of the application file.'
        }
        {
          name: 'ProductName'
          type: 'string'
          description: 'Name of the application product.'
        }
        {
          name: 'ProductInternalName'
          type: 'string'
          description: 'Internal name of the application product.'
        }
        {
          name: 'ProductVersion'
          type: 'string'
          description: 'Version of the application product.'
        }
        {
          name: 'RequestExpiryDateTime'
          type: 'dateTime'
          description: 'Expiration time configured for the elevation request.'
        }
        {
          name: 'ReviewCompletedByUserId'
          type: 'string'
          description: 'Entra object ID of the administrator who reviewed the request.'
        }
        {
          name: 'ReviewCompletedByUserPrincipalName'
          type: 'string'
          description: 'User principal name of the administrator who reviewed the request.'
        }
        {
          name: 'ReviewCompletedDateTime'
          type: 'dateTime'
          description: 'Time the request review was completed.'
        }
        {
          name: 'ReviewerJustification'
          type: 'string'
          description: 'Justification supplied by the reviewing administrator.'
        }
        {
          name: 'IngestionTime'
          type: 'dateTime'
          description: 'Time the collector processed the record.'
        }
        {
          name: 'Source'
          type: 'string'
          description: 'Identifier for the source system.'
        }
      ]
    }
  }
}

output tableId string = elevationRequestsTable.id
output tableName string = elevationRequestsTable.name
