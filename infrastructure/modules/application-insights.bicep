targetScope = 'resourceGroup'

@description('Name of the workspace-based Application Insights component.')
param componentName string

@description('Azure region for the Application Insights component.')
param location string

@description('Resource ID of the existing Log Analytics workspace used by this component.')
param workspaceResourceId string

@description('Tags to apply to the Application Insights component.')
param tags object = {}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: componentName
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    Flow_Type: 'Bluefield'
    IngestionMode: 'LogAnalytics'
    Request_Source: 'rest'
    WorkspaceResourceId: workspaceResourceId
  }
}

output componentId string = applicationInsights.id
output componentName string = applicationInsights.name
