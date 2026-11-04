param([string]$Tag)
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
# Runs GownPoseTests with photographs into unity/Logs/Lighting/poses_<Tag>.
$r=$Repo; $exe=$UnityExe
$env:GOWN_POSE_DIR="$r\unity\Logs\Lighting\poses_$Tag"; New-Item -ItemType Directory -Force $env:GOWN_POSE_DIR | Out-Null
$p=Start-Process $exe -ArgumentList @('-batchmode','-projectPath',"`"$r\unity`"",'-runTests','-testPlatform','PlayMode','-testFilter','GownPoseTests','-testResults',"`"$r\unity\Logs\poses_$Tag.xml`"",'-logFile',"`"$r\unity\Logs\poses_$Tag.log`"") -Wait -PassThru
$xml=[xml](Get-Content "$r\unity\Logs\poses_$Tag.xml"); $tr=$xml.'test-run'; "poses total=$($tr.total) passed=$($tr.passed) failed=$($tr.failed)"
Get-Content "$env:GOWN_POSE_DIR\report.txt" -ErrorAction SilentlyContinue
