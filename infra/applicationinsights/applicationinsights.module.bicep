@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param applicationType string = 'web'

param kind string = 'web'

param logAnalyticsWorkspaceId string

resource applicationinsights 'Microsoft.Insights/components@2020-02-02' = {
  name: take('applicationinsights-${uniqueString(resourceGroup().id)}', 260)
  kind: kind
  location: location
  properties: {
    Application_Type: applicationType
    WorkspaceResourceId: logAnalyticsWorkspaceId
  }
  tags: {
    'aspire-resource-name': 'applicationinsights'
  }
}

output appInsightsConnectionString string = applicationinsights.properties.ConnectionString