param([Parameter(Mandatory=$true)][string]$Run,[string]$Output='Artifacts/DockCoop-Demo.mp4')
$ErrorActionPreference='Stop'
$hostFrames=Join-Path $Run 'frames-Host/frame-%05d.png'
$lateFrames=Join-Path $Run 'frames-Late/frame-%05d.png'
$filter="[0:v]pad=iw:ih+60:0:60:black,drawtext=fontfile='C\:/Windows/Fonts/arial.ttf':text='HOST - impaired peer has +150ms base RTT / 3 percent packet loss':x=20:y=12:fontsize=22:fontcolor=white[h];[1:v]pad=iw:ih+60:0:60:black,drawtext=fontfile='C\:/Windows/Fonts/arial.ttf':text='LATE JOIN - held snapshot / grab and throw / host exit':x=20:y=12:fontsize=22:fontcolor=white[l];[h][l]concat=n=2:v=1:a=0,format=yuv420p[v]"
& ffmpeg -y -loglevel warning -framerate 10 -i $hostFrames -framerate 10 -i $lateFrames -filter_complex $filter -map '[v]' -c:v libx264 -preset medium -crf 20 -movflags +faststart $Output
if($LASTEXITCODE -ne 0){throw 'Video encoding failed'}
Write-Output "Encoded $Output (automated rendered frames, accelerated; no Steam footage)."
