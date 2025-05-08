/* 주석 처리: 전체 파일
이 파일은 AppHost/Program.cs의 builder.AddAzureApplicationInsights() 호출로 대체됩니다.
Aspire는 자동으로 Application Insights 리소스를 생성하고 관리하므로 이 모듈은 불필요합니다.

@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param applicationType string = 'web'

param kind string = 'web'

param logAnalyticsWorkspaceId string

param tags object = {
  'aspire-resource-name': 'applicationinsights'
}

var resourceToken = uniqueString(resourceGroup().id)
var name = 'applicationinsights-${resourceToken}'

resource applicationinsights 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  kind: kind
  location: location
  properties: {
    Application_Type: applicationType
    WorkspaceResourceId: logAnalyticsWorkspaceId
    DisableLocalAuth: false
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    RetentionInDays: 90
  }
  tags: tags
}

output appInsightsConnectionString string = applicationinsights.properties.ConnectionString
output appInsightsInstrumentationKey string = applicationinsights.properties.InstrumentationKey
output appInsightsName string = applicationinsights.name
output appInsightsId string = applicationinsights.id
*/
