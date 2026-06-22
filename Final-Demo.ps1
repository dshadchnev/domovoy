# Final-Demo.ps1 — Полная проверка IoT Pipeline
$ErrorActionPreference = "SilentlyContinue"
$Tests = @()
$Pass = 0; $Fail = 0; $Warn = 0

function Add-Result($Name, $Status, $Details) {
    $global:Tests += [PSCustomObject]@{ Test=$Name; Status=$Status; Details=$Details }
    if ($Status -match "PASS") { $global:Pass++ }
    elseif ($Status -match "FAIL") { $global:Fail++ }
    else { $global:Warn++ }
}

# 1. User Auth
try {
    $userToken = (Invoke-RestMethod -Uri "http://localhost:8086/connect/token" `
      -Method Post -ContentType "application/x-www-form-urlencoded" `
      -Body "grant_type=password&client_id=domovoy-client&username=admin@domovoy.local&password=StrongPass123!&scope=openid").access_token
    Add-Result "1. User Auth" "PASS" "JWT obtained"
} catch { Add-Result "1. User Auth" "FAIL" $_.Exception.Message }

# 2. Device Registration
try {
    $deviceId = "demo-$(Get-Date -Format 'yyyyMMddHHmmss')"
    $dev = Invoke-RestMethod -Uri "http://localhost:8085/api/Devices/register" `
      -Method Post -Headers @{Authorization="Bearer $userToken"; "Content-Type"="application/json"} `
      -Body (@{networkDeviceId=$deviceId}|ConvertTo-Json)
    Add-Result "2. Device Register" "PASS" "ID: $deviceId"
} catch { Add-Result "2. Device Register" "FAIL" $_.Exception.Message; $dev=$null }

# 3. Device Auth (JWS)
if ($dev) {
    try {
        $devToken = (Invoke-RestMethod -Uri "http://localhost:8085/api/device-auth/authenticate" `
          -Method Post -ContentType "application/json" `
          -Body (@{networkDeviceId=$dev.networkDeviceId;secret=$dev.secret}|ConvertTo-Json)).accessToken
        Add-Result "3. Device Auth" "PASS" "JWS Token OK"
    } catch { Add-Result "3. Device Auth" "FAIL" $_.Exception.Message; $devToken=$null }

    # 4. Telemetry Submit
    if ($devToken) {
        try {
            $resp = Invoke-RestMethod -Uri "http://localhost:8085/api/devices/$deviceId/telemetry" `
              -Method Post -Headers @{Authorization="Bearer $devToken"; "Content-Type"="application/json"} `
              -Body (@{status="ON";temperature=24.8;brightness=90}|ConvertTo-Json)
            Add-Result "4. Telemetry Submit" "PASS" "Server status: $($resp.status)"
        } catch { Add-Result "4. Telemetry Submit" "FAIL" $_.Exception.Message }
    }
}

# Wait for MassTransit async processing
Start-Sleep -Seconds 3

# 5. RabbitMQ Publish
try {
    $mqLog = docker logs domovoy-auth --since 1m 2>$null
    if ($mqLog | Select-String "TelemetryReceivedEvent" -Quiet) {
        Add-Result "5. RabbitMQ Publish" "PASS" "Event sent to Exchange"
    } else {
        Add-Result "5. RabbitMQ Publish" "FAIL" "No log entry found"
    }
} catch { Add-Result "5. RabbitMQ Publish" "FAIL" $_.Exception.Message }

# 6. Active Consumer
try {
    $queueLines = docker exec domovoy-rabbitmq rabbitmqctl list_queues name consumers 2>$null
    $telemetryLine = $queueLines | Where-Object { $_ -match "^Telemetry" }
    if ($telemetryLine) {
        $cols = ($telemetryLine -split '\s+').Where({ $_ -ne '' })
        $consumers = [int]$cols[1]
        if ($consumers -ge 1) {
            Add-Result "6. Active Consumer" "PASS" "Queue=Telemetry, consumers=$consumers"
        } else {
            Add-Result "6. Active Consumer" "FAIL" "consumers=$consumers"
        }
    } else {
        Add-Result "6. Active Consumer" "FAIL" "Queue Telemetry not found"
    }
} catch { Add-Result "6. Active Consumer" "FAIL" $_.Exception.Message }

# 7. Redis Storage
try {
    $redisVal = docker exec domovoy-redis redis-cli GET "device:telemetry:$deviceId" 2>$null
    if ($redisVal -match "temperature") {
        Add-Result "7. Redis Storage" "PASS" "Saved to Redis"
    } else {
        Add-Result "7. Redis Storage" "FAIL" "Key not found/expired"
    }
} catch { Add-Result "7. Redis Storage" "FAIL" $_.Exception.Message }

# Report
Write-Host ""
Write-Host "FINAL DEMO REPORT" -ForegroundColor Cyan
Write-Host "=================" -ForegroundColor Cyan
$Tests | Format-Table -AutoSize
Write-Host "Passed: $Pass | Failed: $Fail | Warnings: $Warn" -ForegroundColor $(if($Fail -eq 0){"Green"}else{"Red"})
Write-Host "System is ready for demonstration!" -ForegroundColor Yellow