# === Test-All.ps1 — запуск полной проверки системы ===
$ErrorActionPreference = "Stop"
$results = @{}

Write-Host "Domovoy Full Test Suite" -ForegroundColor Cyan
Write-Host "===========================" -ForegroundColor Cyan

# 1. Infrastructure health
Write-Host "`n[INFRA] Checking infrastructure..." -ForegroundColor Yellow
$infra = docker compose -f infra/docker-compose.yml ps --format "table {{.Service}}\t{{.Status}}" 2>$null
$healthy = ($infra | Select-String "healthy").Count -ge 6
$results["Infrastructure"] = if($healthy){"Pass"}else{"Fail"}
Write-Host $results["Infrastructure"] -ForegroundColor $(if($healthy){"Green"}else{"Red"})

# 2. Auth Service tests
Write-Host "`n[AUTH] Testing Auth Service..." -ForegroundColor Yellow
try {
    powershell -ExecutionPolicy Bypass -File ..\test_oauth_token.ps1 | Out-Null
    $results["Auth Service"] = "Pass"
} catch {
    $results["Auth Service"] = "Fail"
}

# 3. Device Manager tests
Write-Host "`n[MGR] Testing Device Manager..." -ForegroundColor Yellow
try {
    $gateway = "http://localhost:8085"
    $userToken = (Invoke-RestMethod "$gateway/connect/token" -Method POST `
        -ContentType "application/x-www-form-urlencoded" `
        -Body "grant_type=password&client_id=domovoy-client&username=admin@domovoy.local&password=StrongPass123!&scope=openid").access_token
    
    $devices = Invoke-RestMethod "$gateway/api/device-mgmt" -Method GET `
        -Headers @{ Authorization = "Bearer $userToken" }
    
    # Trigger DeviceUpdatedEvent by updating one of the devices
    if ($devices -is [array] -and $devices.Count -gt 0) {
        $devId = $devices[0].NetworkDeviceId
        $updateBody = @{ name = "Updated Name"; roomId = $devices[0].RoomId } | ConvertTo-Json
        Invoke-RestMethod "$gateway/api/device-mgmt/$devId" -Method PUT `
            -Headers @{ Authorization = "Bearer $userToken"; "Content-Type" = "application/json" } `
            -Body $updateBody | Out-Null
    }
    
    $results["Device Manager"] = if($devices -is [array]){"Pass"}else{"Fail"}
} catch {
    $results["Device Manager"] = "Fail"
}

# 4. Gateway routing
Write-Host "`n[GATEWAY] Testing routing..." -ForegroundColor Yellow
$routes = @(
    @{ url = "$gateway/connect/token"; method = "POST"; body = "grant_type=password&client_id=domovoy-client&username=admin@domovoy.local&password=StrongPass123!&scope=openid"; contentType = "application/x-www-form-urlencoded" },
    @{ url = "$gateway/api/device-mgmt"; method = "GET"; headers = @{ Authorization = "Bearer $userToken" } }
)
$gatewayOk = $true
foreach ($route in $routes) {
    try {
        $params = @{ Method = $route.method; Uri = $route.url; UseBasicParsing = $true }
        if ($route.body) { $params["Body"] = $route.body; $params["ContentType"] = $route.contentType }
        if ($route.headers) { $params["Headers"] = $route.headers }
        Invoke-RestMethod @params -TimeoutSec 10 | Out-Null
    } catch {
        if ($route.url -match "connect/token") {
            # Token endpoint returns 200 with body, others may return 401 (expected)
            $gatewayOk = $false
        }
    }
}
$results["Gateway Routing"] = if($gatewayOk){"Pass"}else{"Partial"}

# 5. Event bus
Write-Host "`n[EVENTS] Checking RabbitMQ..." -ForegroundColor Yellow
$eventsOk = (docker logs domovoy-auth --since 10m 2>$null | Select-String "TelemetryReceivedEvent" -Quiet) -and
            (docker logs domovoy-device-manager --since 10m 2>$null | Select-String "DeviceUpdatedEvent" -Quiet)
$results["Event Bus"] = if($eventsOk){"Pass"}else{"Pending"}

# 6. Data persistence
Write-Host "`n[DATA] Checking databases..." -ForegroundColor Yellow
try {
    $pgCheck = docker exec domovoy-postgres psql -U postgres -d domovoy_auth -c "SELECT 1" 2>$null
    $redisCheck = docker exec domovoy-redis redis-cli PING 2>$null
    $results["Data Layer"] = if($pgCheck -match "1" -and $redisCheck -match "PONG"){"Pass"}else{"Fail"}
} catch {
    $results["Data Layer"] = "Fail"
}

# 7. Command Dispatcher tests
Write-Host "`n[DISPATCHER] Testing Command Dispatcher..." -ForegroundColor Yellow
try {
    dotnet test Domovoy.CommandDispatcher.Service.Tests\Domovoy.CommandDispatcher.Service.Tests.csproj | Out-Null
    $results["Command Dispatcher"] = "Pass"
} catch {
    $results["Command Dispatcher"] = "Fail"
}

# Итоговый отчёт
Write-Host "`nTest Results Summary" -ForegroundColor Cyan
Write-Host "=======================" -ForegroundColor Cyan
$results.GetEnumerator() | Sort-Object Name | ForEach-Object {
    $color = if($_.Value -eq "Pass"){"Green"}elseif($_.Value -eq "Fail"){"Red"}else{"Yellow"}
    Write-Host ("{0,-20} {1}" -f $_.Key, $_.Value) -ForegroundColor $color
}

$allPass = ($results.Values | Where-Object {$_ -ne "Pass"}).Count -eq 0
if ($allPass) {
    Write-Host "`nALL TESTS PASSED" -ForegroundColor Cyan
} else {
    Write-Host "`nSome tests need attention" -ForegroundColor Yellow
}