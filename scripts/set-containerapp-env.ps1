param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupPrefix,
    
    [Parameter(Mandatory=$true)]
    [string]$ApplicationInsightsConnectionString
)

# 리소스 그룹 이름 설정
$resourceGroup = "rg-$ResourceGroupPrefix"

Write-Host "🔍 리소스 그룹 $resourceGroup에서 Container Apps 찾는 중..."

# 리소스 그룹 존재 여부 확인
$rgExists = (az group exists --name "$resourceGroup" 2>/dev/null) -eq "true"
if (-not $rgExists) {
  # 다른 이름 패턴 시도
  $possibleGroups = az group list --query "[?contains(name,'$ResourceGroupPrefix') || contains(name,'interview') || contains(name,'capstone')].name" -o tsv
  if ($possibleGroups) {
    $resourceGroup = $possibleGroups -split "\n" | Select-Object -First 1
    Write-Host "✅ 리소스 그룹 찾음: $resourceGroup"
  } else {
    Write-Host "❌ 리소스 그룹을 찾을 수 없습니다"
    exit 1
  }
}

# Container Apps 목록 조회
$containerApps = az containerapp list --resource-group $resourceGroup --query "[].name" -o tsv
if ([string]::IsNullOrEmpty($containerApps)) {
  Write-Host "❌ Container Apps를 찾을 수 없습니다"
  exit 1
}

# 각 Container App에 환경 변수 설정
foreach ($app in $containerApps -split "\n") {
  if (-not [string]::IsNullOrWhiteSpace($app)) {
    Write-Host "🔧 '$app' 앱에 환경 변수 설정 중..."
    
    az containerapp update --name "$app" --resource-group "$resourceGroup" `
      --set-env-vars "APPLICATIONINSIGHTS_CONNECTION_STRING=$ApplicationInsightsConnectionString" `
                    "ApplicationInsights__ConnectionString=$ApplicationInsightsConnectionString" `
                    "ConnectionStrings__ApplicationInsights=$ApplicationInsightsConnectionString" `
                    --only-show-errors
    
    if ($LASTEXITCODE -eq 0) {
      Write-Host "✅ '$app' 환경 변수 설정 성공"
    } else {
      Write-Host "❌ '$app' 환경 변수 설정 실패"
    }
  }
}

Write-Host "✅ Container Apps 환경 변수 설정 완료"
