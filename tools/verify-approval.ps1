param(
    [string]$Build = (Join-Path $PSScriptRoot '../Builds/Windows/DockCoop.exe'),
    [int]$Port = 17877,
    [int]$MaxPlayers = 4
)
$ErrorActionPreference = 'Stop'
$Build = (Resolve-Path -LiteralPath $Build).Path
$runId = 'approval-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
$logRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../Artifacts/$runId"))
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$processes = [Collections.Generic.List[System.Diagnostics.Process]]::new()

function Start-Client([string]$Label, [string[]]$Extra) {
    $logPath = Join-Path $logRoot "$Label.log"
    $arguments = @('-batchmode', '-nographics', '-logFile', $logPath, '--port', "$Port", '--address', '127.0.0.1', '--name', $Label) + $Extra
    $process = Start-Process -FilePath $Build -WorkingDirectory (Split-Path $Build) -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $processes.Add($process)
    return @{ Process = $process; Log = $logPath }
}

function Wait-Log($Client, [string]$Text, [int]$Seconds = 35) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ((Test-Path -LiteralPath $Client.Log) -and (Select-String -LiteralPath $Client.Log -SimpleMatch $Text -Quiet)) { return }
        if ($Client.Process.HasExited) { throw "Process exited without '$Text': $($Client.Log)" }
        Start-Sleep -Milliseconds 200
    }
    throw "Timed out waiting for '$Text': $($Client.Log)"
}

function Assert-Rejected([string]$Label, [string[]]$Extra, [string]$Reason) {
    $client = Start-Client $Label (@('--join', '--smoke', '--expect-rejection', $Reason) + $Extra)
    Wait-Log $client 'SMOKE_REJECTION_PASS'
    if (-not $client.Process.WaitForExit(10000)) { throw "$Label did not exit after success" }
    if ($client.Process.ExitCode -ne 0) { throw "$Label returned exit code $($client.Process.ExitCode)" }
    Write-Output "PASS $Label"
}

try {
    $hostClient = Start-Client 'host' @('--host', '--profile', "$runId-host", '--max-players', "$MaxPlayers")
    Wait-Log $hostClient '[Session] Connected.'
    Assert-Rejected 'duplicate' @('--profile', "$runId-host") 'already'
    Assert-Rejected 'version' @('--profile', "$runId-version", '--build-version', 'incompatible') 'Different'
    for ($i = 1; $i -lt $MaxPlayers; $i++) {
        $client = Start-Client "player$i" @('--join', '--profile', "$runId-player$i")
        Wait-Log $client '[Session] Connected.'
    }
    Assert-Rejected 'full' @('--profile', "$runId-fifth") 'full'
    Assert-Rejected 'timeout' @('--profile', "$runId-timeout", '--port', "$($Port + 1)") 'timed'
    Write-Output "Approval integration checks passed. Logs: $logRoot"
}
finally {
    foreach ($process in $processes) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        $process.Dispose()
    }
}
