param(
    [string]$Build = (Join-Path $PSScriptRoot '../Builds/Windows/DockCoop.exe'),
    [int]$Port = 17892
)
$ErrorActionPreference = 'Stop'
$Build = (Resolve-Path -LiteralPath $Build).Path
$runId = 'loading-disconnect-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
$logRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../Artifacts/$runId"))
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$owned = [Collections.Generic.List[System.Diagnostics.Process]]::new()

function Start-Player([string]$Label, [string[]]$Extra) {
    $logPath = Join-Path $logRoot "$Label.log"
    $arguments = @('-batchmode', '-nographics', '--transport', 'utp', '--profile', "$runId-$Label", '--name', $Label,
        '--port', "$Port", '--address', '127.0.0.1', '--smoke', '-logFile', ('"' + $logPath + '"')) + $Extra
    $process = Start-Process -FilePath $Build -WorkingDirectory (Split-Path $Build) -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $owned.Add($process)
    Write-Host "START $Label pid=$($process.Id)"
    return @{ Process = $process; Log = $logPath }
}

function Wait-Marker($Player, [string]$Marker, [int]$Seconds = 35) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Player.Log) {
            $content = Get-Content -LiteralPath $Player.Log -Raw
            if ($content -match 'SMOKE_FAIL|NullReferenceException|InvalidOperationException|Failed to spawn NetworkObject') {
                throw "Player failure: $($Player.Log)"
            }
            if ($content.Contains($Marker)) { Write-Output "PASS $Marker"; return }
        }
        if ($Player.Process.HasExited) { throw "Player exited without '$Marker': $($Player.Log)" }
        Start-Sleep -Milliseconds 200
    }
    throw "Timed out waiting for '$Marker': $($Player.Log)"
}

try {
    $hostPlayer = Start-Player 'host' @('--host', '--wait-players', '3', '--expect-loading-disconnect')
    Wait-Marker $hostPlayer 'SMOKE_CONNECTED'
    $heldClient = Start-Player 'loading-client' @('--join', '--hold-loading')
    Wait-Marker $heldClient 'SMOKE_CONNECTED'
    $survivor = Start-Player 'survivor' @('--join', '--stay-seconds', '60')
    Wait-Marker $heldClient 'SMOKE_LOADING_HELD'
    Wait-Marker $hostPlayer 'SMOKE_LOADING_BARRIER_WAITING'
    if ($heldClient.Process.HasExited) { throw 'Loading client exited before the controlled crash.' }
    "FORCE_LOADING_CLIENT pid=$($heldClient.Process.Id) hostPid=$($hostPlayer.Process.Id) transport=utp phase=Loading ready=2/3" |
        Tee-Object -FilePath (Join-Path $logRoot 'harness.log') | Out-Host
    Stop-Process -Id $heldClient.Process.Id -Force
    Wait-Marker $hostPlayer 'SMOKE_LOADING_DISCONNECT_PASS players=2'
    Wait-Marker $survivor 'SMOKE_PLAYING players=2'
    "LOADING_DISCONNECT_SUITE_PASS $logRoot" | Tee-Object -FilePath (Join-Path $logRoot 'harness.log') -Append | Write-Output
}
finally {
    foreach ($process in $owned) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
        $process.Dispose()
    }
}
