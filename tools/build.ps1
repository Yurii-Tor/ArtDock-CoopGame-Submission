param([string]$Unity='C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$log=Join-Path $root 'Artifacts/build.log'
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
$p=Start-Process -FilePath $Unity -ArgumentList '-batchmode','-nographics','-quit','-projectPath',('"'+$root+'"'),'-executeMethod','Coop.Editor.ProjectBuilder.BuildWindows','-logFile',('"'+$log+'"') -WindowStyle Hidden -Wait -PassThru
if($p.ExitCode -ne 0 -or !(Select-String -Path $log -Pattern 'COOP_BUILD_OK' -Quiet)){throw "Build failed; inspect $log"}
Write-Output "Built $root/Builds/Windows/DockCoop.exe"
