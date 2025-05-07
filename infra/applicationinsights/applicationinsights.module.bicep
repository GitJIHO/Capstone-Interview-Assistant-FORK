@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param applicationType string = 'web'

param kind string = 'web'

param logAnalyticsWorkspaceId string

// 태그 매개변수 추가
param tags object = {
  'aspire-resource-name': 'applicationinsights'
}

// 고유한 이름 생성 방식 수정
var resourceToken = uniqueString(resourceGroup().id)
var name = 'appi-${resourceToken}'

resource applicationinsights 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  kind: kind
  location: location
  properties: {
    Application_Type: applicationType
    WorkspaceResourceId: logAnalyticsWorkspaceId
    // 추가 속성 설정
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
