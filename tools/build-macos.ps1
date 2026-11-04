param([string]$Unity='C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$log=Join-Path $root 'Artifacts/build-macos.log'
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
$p=Start-Process -FilePath $Unity -ArgumentList '-batchmode','-nographics','-quit','-buildTarget','OSXUniversal','-projectPath',('"'+$root+'"'),'-executeMethod','Coop.Editor.ProjectBuilder.BuildMacOS','-logFile',('"'+$log+'"') -WindowStyle Hidden -PassThru
$p.WaitForExit()
if($p.ExitCode -ne 0 -or !(Select-String -Path $log -Pattern 'COOP_MAC_BUILD_OK' -Quiet)){throw "macOS build failed; inspect $log"}
Write-Output "Built $root/Builds/macOS/DockCoop.app"
