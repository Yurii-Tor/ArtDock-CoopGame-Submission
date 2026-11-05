param(
    [string]$Build = (Join-Path $PSScriptRoot '../Builds/Windows/DockCoop.exe'),
    [int]$Port = 17890,
    [switch]$Capture
)
$ErrorActionPreference = 'Stop'
$Build = (Resolve-Path -LiteralPath $Build).Path
$runId = 'host-crash-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
$logRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../Artifacts/$runId"))
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$owned = [Collections.Generic.List[System.Diagnostics.Process]]::new()

function Start-Player([string]$Label, [string[]]$Extra, [bool]$Graphics = $false) {
    $logPath = Join-Path $logRoot "$Label.log"
    $arguments = @('--transport', 'utp', '--profile', "$runId-$Label", '--name', $Label,
        '--port', "$Port", '--address', '127.0.0.1', '--smoke', '-logFile', ('"' + $logPath + '"')) + $Extra
    if ($Graphics) {
        $arguments += @('-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
            '--capture', ('"' + (Join-Path $logRoot 'frames-client') + '"'))
    } else { $arguments = @('-batchmode', '-nographics') + $arguments }
    $process = Start-Process -FilePath $Build -WorkingDirectory (Split-Path $Build) -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $owned.Add($process)
    "START $Label pid=$($process.Id)" | Tee-Object -FilePath (Join-Path $logRoot 'harness.log') -Append | Out-Host
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
    $hostPlayer = Start-Player 'host' @('--host', '--stay-seconds', '90')
    Wait-Marker $hostPlayer 'SMOKE_CONNECTED'
    $client = Start-Player 'client' @('--join', '--expect-host-crash') $Capture.IsPresent
    Wait-Marker $client 'SMOKE_PLAYING'
    Wait-Marker $hostPlayer 'SMOKE_PLAYING'
    if ($hostPlayer.Process.HasExited) { throw 'Host exited before the controlled crash.' }
    "FORCE_HOST pid=$($hostPlayer.Process.Id) clientPid=$($client.Process.Id) transport=utp clientWasPlaying=true" |
        Tee-Object -FilePath (Join-Path $logRoot 'harness.log') -Append | Write-Output
    Stop-Process -Id $hostPlayer.Process.Id -Force
    Wait-Marker $client 'SMOKE_HOST_CRASH_MODEL_PASS'
    Wait-Marker $client 'SMOKE_HOST_CRASH_UI_STATE_PASS'
    Wait-Marker $client 'SMOKE_HOST_CRASH_REHOST_PASS'
    if (-not $client.Process.WaitForExit(10000)) { throw 'Client did not exit after verification.' }
    if ($client.Process.ExitCode -ne 0) { throw "Client returned $($client.Process.ExitCode)." }
    "HOST_CRASH_SUITE_PASS $logRoot" | Tee-Object -FilePath (Join-Path $logRoot 'harness.log') -Append | Write-Output
    Write-Output 'UI-state assertions are automated; inspect the optional captured frames for visual evidence.'
}
finally {
    foreach ($process in $owned) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
        $process.Dispose()
    }
}
