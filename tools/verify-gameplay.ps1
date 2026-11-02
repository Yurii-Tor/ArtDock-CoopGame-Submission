param([switch]$Capture,[switch]$NoDisconnect,[switch]$KeepImpaired)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$exe=Join-Path $root 'Builds/Windows/DockCoop.exe'
$logs=Join-Path $root ('Artifacts/gameplay-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $logs | Out-Null
$owned=[System.Collections.Generic.List[System.Diagnostics.Process]]::new()
function Start-Player([string]$name,[string[]]$extra,[bool]$graphics=$false) {
    $log=Join-Path $logs ($name+'.log')
    $arguments=@('--profile',$name,'--name',$name,'--smoke','-logFile',('"'+$log+'"'))+$extra
    if(!$graphics){$arguments=@('-batchmode','-nographics')+$arguments}
    else {$arguments+=@('-force-d3d11','-screen-width','1280','-screen-height','720','-screen-fullscreen','0','--capture',('"'+(Join-Path $logs ('frames-'+$name))+'"'))}
    $p=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $owned.Add($p)
    Write-Host "START $name pid=$($p.Id)"
    return $p
}
function Wait-Marker([string]$name,[string]$marker,[int]$seconds=45){
    $until=[DateTime]::UtcNow.AddSeconds($seconds)
    $path=Join-Path $logs ($name+'.log')
    while([DateTime]::UtcNow -lt $until){
        if(Test-Path $path){
            $text=Get-Content -Raw $path
            if($text -match 'SMOKE_FAIL|NullReferenceException|Failed to spawn NetworkObject'){throw "Failure in $name; see $path"}
            if($text.Contains($marker)){Write-Output "PASS $name $marker";return}
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Timeout waiting for $name $marker; see $path"
}
try {
    $proxy=Start-Process -FilePath 'python' -ArgumentList ('"'+(Join-Path $PSScriptRoot 'udp_proxy.py')+'"'),'--listen','17881','--target','17880','--delay-ms','75','--jitter-ms','15','--loss','.03' -RedirectStandardOutput (Join-Path $logs 'proxy.log') -RedirectStandardError (Join-Path $logs 'proxy-error.log') -WindowStyle Hidden -PassThru
    $owned.Add($proxy)
    $hostPlayer=Start-Player 'Host' @('--host','--port','17880','--stay-seconds','40','--net-debug') $Capture.IsPresent
    $stay=if($NoDisconnect -or $KeepImpaired){'60'}else{'12'}
    $impaired=Start-Player 'Impaired' @('--join','--port','17881','--stay-seconds',$stay)
    Wait-Marker 'Impaired' 'SMOKE_THROWN'
    $holder=Start-Player 'Holder' @('--join','--port','17880','--hold-exit','--hold-seconds','30')
    Wait-Marker 'Holder' 'SMOKE_HOLD_WAIT'
    $late=Start-Player 'Late' @('--join','--port','17880','--expect-held','--expect-host-loss','--stay-seconds','60') $Capture.IsPresent
    Wait-Marker 'Late' 'SMOKE_LATEJOIN_HELD_PASS'
    if(!$NoDisconnect){
        Write-Host "STOP Holder pid=$($holder.Id)"
        Stop-Process -Id $holder.Id -Force
        Wait-Marker 'Host' 'Disconnect drop' 25
        if(!$KeepImpaired){Wait-Marker 'Impaired' 'SMOKE_PASS'}
        $fresh=Start-Player 'Fresh' @('--join','--port','17880','--stay-seconds','6')
        Wait-Marker 'Fresh' 'SMOKE_THROWN'
        Wait-Marker 'Fresh' 'SMOKE_PASS'
    }
    Wait-Marker 'Host' 'SMOKE_PASS' 60
    Wait-Marker 'Late' 'SMOKE_HOST_LOSS_PASS' 30
    Write-Output "GAMEPLAY_SUITE_PASS $logs"
}
finally {
    foreach($p in $owned){if(!$p.HasExited){Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue}}
}
