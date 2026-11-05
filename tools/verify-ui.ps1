param(
    [string]$Build = (Join-Path $PSScriptRoot '../Builds/Windows/DockCoop.exe'),
    [ValidateRange(1, 65535)][int]$Port = 17977,
    [switch]$SteamAvailable,
    [switch]$Capture
)
$ErrorActionPreference = 'Stop'
$Build = (Resolve-Path -LiteralPath $Build).Path
if (-not $SteamAvailable -and (Get-Process -Name steam -ErrorAction SilentlyContinue)) {
    throw 'Default checks require Steam to be closed. Close it yourself or use -SteamAvailable for the signed-in cases. This script never stops Steam.'
}
$runId = 'ui-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
$logRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../Artifacts/$runId"))
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$owned = [Collections.Generic.List[System.Diagnostics.Process]]::new()
$modes = if ($SteamAvailable) { @('utp-port', 'steam-invalid-lobby', 'steam-host') } else { @('utp-port', 'steam-unavailable') }

function Assert-Log([string]$Content, [string]$Mode, [string]$Log) {
    foreach ($line in ($Content -split '\r?\n')) {
        # Invalid lobby IDs deliberately exercise the adapter's validation exception.
        if ($Mode -eq 'steam-invalid-lobby' -and
            $line -match '^System\.InvalidOperationException: Enter the numeric Steam lobby ID\.$') { continue }
        if ($line -match 'UI_PROBE_FAIL|NullReferenceException|Exception:|Failed to spawn NetworkObject|error CS\d+|Assertion failed') {
            throw "UI regression error in ${Log}: $line"
        }
    }
}

try {
    foreach ($mode in $modes) {
        $log = Join-Path $logRoot "$mode.log"
        $transport = if ($mode -eq 'utp-port') { 'utp' } else { 'steam' }
        $portArgument = if ($transport -eq 'steam') { 'garbage' } else { "$Port" }
        $arguments = @('--transport', $transport, '--profile', "$runId-$mode", '--name', 'UI-check',
            '--ui-probe', $mode, '--port', $portArgument, '--probe-port', "$Port", '-logFile', ('"' + $log + '"'))
        if ($Capture) {
            $frames = Join-Path $logRoot "frames-$mode"
            $arguments += @('-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
                '--capture', ('"' + $frames + '"'))
        } else { $arguments = @('-batchmode', '-nographics') + $arguments }
        $process = Start-Process -FilePath $Build -WorkingDirectory (Split-Path $Build) -ArgumentList $arguments -WindowStyle Hidden -PassThru
        $owned.Add($process)
        "START $mode pid=$($process.Id)" | Tee-Object -FilePath (Join-Path $logRoot 'harness.log') -Append | Out-Host
        $deadline = [DateTime]::UtcNow.AddSeconds(45)
        $marker = "UI_PROBE_PASS $mode"
        $passed = $false
        while ([DateTime]::UtcNow -lt $deadline) {
            if (Test-Path -LiteralPath $log) {
                $content = [string](Get-Content -LiteralPath $log -Raw)
                Assert-Log $content $mode $log
                if ($content.Contains($marker)) { $passed = $true; break }
            }
            if ($process.HasExited) { throw "Process exited before '$marker': $log" }
            Start-Sleep -Milliseconds 200
        }
        if (-not $passed) { throw "Timed out waiting for '$marker': $log" }
        if (-not $process.WaitForExit(10000)) { throw "Process did not exit after '$marker': $log" }
        if ($process.ExitCode -ne 0) { throw "UI probe returned $($process.ExitCode): $log" }
        Assert-Log (Get-Content -LiteralPath $log -Raw) $mode $log
        if ($Capture -and -not (Get-ChildItem -LiteralPath $frames -Filter 'frame-*.png' -ErrorAction SilentlyContinue)) {
            throw "Capture requested but no rendered frames were saved: $frames"
        }
        "PASS $mode" | Tee-Object -FilePath (Join-Path $logRoot 'harness.log') -Append | Write-Output
    }
    "UI_SUITE_PASS $logRoot" | Tee-Object -FilePath (Join-Path $logRoot 'harness.log') -Append | Write-Output
    if ($Capture) { Write-Output 'Inspect frames-* for rendered UI evidence in addition to the automated text/layout assertions.' }
}
finally {
    foreach ($process in $owned) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
        $process.Dispose()
    }
}
