param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupPrefix,
    
    [Parameter(Mandatory=$true)]
    [string]$ApplicationInsightsConnectionString,
    
    [Parameter(Mandatory=$false)]
    [string]$ClientId,
    
    [Parameter(Mandatory=$false)]
    [string]$TenantId,
    
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId
)

# Azure CLI 로그인 시도 (GitHub Action에서 실행 시)
Write-Host "🔐 Azure CLI로 로그인 시도 중..."
if (-not [string]::IsNullOrEmpty($ClientId) -and -not [string]::IsNullOrEmpty($TenantId)) {
    try {
        Write-Host "GitHub Action 환경에서 페더레이션 자격 증명을 사용하여 로그인 중..."
        # GitHub Actions 환경에서 페더레이션 자격 증명으로 로그인
        az login --service-principal --username $ClientId --tenant $TenantId --federated-token $env:ACTIONS_ID_TOKEN_REQUEST_TOKEN
        
        if ($LASTEXITCODE -ne 0) {
            throw "페더레이션 자격 증명을 사용한 로그인 실패"
        }
        
        # 구독 지정
        if (-not [string]::IsNullOrEmpty($SubscriptionId)) {
            Write-Host "Azure 구독 설정 중: $SubscriptionId"
            az account set --subscription $SubscriptionId
        }
        
        Write-Host "✅ Azure 로그인 성공"
    }
    catch {
        Write-Host "⚠️ 페더레이션 자격 증명으로 로그인 실패, 'az login'이 이미 실행되었는지 확인합니다..."
    }
}

# Azure CLI 로그인 상태 확인
Write-Host "🔍 Azure CLI 로그인 상태 확인 중..."
$loginStatus = az account show 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Azure CLI에 로그인되어 있지 않습니다. 이 스크립트를 실행하기 전에 'az login'을 실행하세요."
    Write-Host "로그인 오류: $loginStatus"
    exit 1
}
Write-Host "✅ Azure 로그인 확인 완료"

# 리소스 그룹 이름 설정
$resourceGroup = "rg-$ResourceGroupPrefix"

Write-Host "🔍 리소스 그룹 $resourceGroup에서 Container Apps 찾는 중..."

# 리소스 그룹 존재 여부 확인
try {
    $rgExists = (az group exists --name "$resourceGroup" 2>/dev/null) -eq "true"
    if (-not $rgExists) {
        # 다른 이름 패턴 시도
        Write-Host "🔍 대체 리소스 그룹 검색 중..."
        $possibleGroups = az group list --query "[?contains(name,'$ResourceGroupPrefix') || contains(name,'interview') || contains(name,'capstone')].name" -o tsv
        if ($possibleGroups) {
            $resourceGroup = $possibleGroups -split "\n" | Select-Object -First 1
            Write-Host "✅ 리소스 그룹 찾음: $resourceGroup"
        } else {
            Write-Host "❌ 리소스 그룹을 찾을 수 없습니다."
            exit 1
        }
    }
} catch {
    Write-Host "❌ 리소스 그룹 확인 중 오류 발생: $_"
    exit 1
}

# Container Apps 목록 조회
try {
    Write-Host "🔍 Container Apps 목록 조회 중..."
    $containerApps = az containerapp list --resource-group $resourceGroup --query "[].name" -o tsv
    if ([string]::IsNullOrEmpty($containerApps)) {
        Write-Host "❌ Container Apps를 찾을 수 없습니다"
        exit 1
    }
    Write-Host "✅ Container Apps 목록 조회 완료"
} catch {
    Write-Host "❌ Container Apps 목록 조회 중 오류 발생: $_"
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
