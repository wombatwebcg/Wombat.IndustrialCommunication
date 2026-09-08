$ErrorActionPreference = 'Stop'
$env:Auth__JwtKey = '01234567890123456789012345678901'
$env:BOOTSTRAP_ADMIN_PASSWORD = 'HistorySmokePassword1!'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5192'
$dbPath = Join-Path $env:TEMP "wombat-gateway-history-$PID.db3"
$env:SqlConfig__FreeSqlCollections__0__MasterConnetion = "Data Source=$dbPath"
$exe = Join-Path $PSScriptRoot 'bin\Debug\net10.0\Wombat.IndustrialCommunication.Gateway.exe'
$process = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
try {
    Start-Sleep -Seconds 2
    $login = Invoke-RestMethod -Uri 'http://127.0.0.1:5192/api/auth/login' -Method Post -ContentType 'application/json' -Body '{"userName":"admin","password":"HistorySmokePassword1!"}'
    $headers = @{ Authorization = "Bearer $($login.accessToken)" }
    $channel = Invoke-RestMethod -Uri 'http://127.0.0.1:5192/api/channels' -Method Post -Headers $headers -ContentType 'application/json' -Body '{"name":"history-smoke","protocol":"ModbusTcp","role":"Client","enabled":false,"configuration":{}}'
    $point = Invoke-RestMethod -Uri "http://127.0.0.1:5192/api/channels/$($channel.id)/points" -Method Post -Headers $headers -ContentType 'application/json' -Body '{"name":"history-point","address":"40001","dataType":"UInt16","writable":true,"enabled":false}'
    Invoke-RestMethod -Uri "http://127.0.0.1:5192/api/history/policies/$($point.id)" -Method Put -Headers $headers -ContentType 'application/json' -Body '{"enabled":true,"aggregateIntervalSeconds":60,"rawRetentionDays":1,"aggregateRetentionDays":1}' | Out-Null
    $policy = Invoke-RestMethod -Uri "http://127.0.0.1:5192/api/history/policies?pointId=$($point.id)" -Headers $headers
    $logins = Invoke-RestMethod -Uri 'http://127.0.0.1:5192/api/logs/login?userName=admin' -Headers $headers
    if ($policy.Count -ne 1 -or $logins.Count -lt 1) { throw 'History API smoke check failed' }
    Write-Output 'History API smoke check passed'
}
finally {
    if (!$process.HasExited) { Stop-Process -Id $process.Id -Force; try { $process.WaitForExit() } catch {} }
    if (Test-Path -LiteralPath $dbPath) { Remove-Item -LiteralPath $dbPath -Force }
}
