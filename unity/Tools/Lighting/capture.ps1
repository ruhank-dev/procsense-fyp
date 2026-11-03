param([string]$Tag)
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
# PlayMode capture of all LightingCaptureTools views into unity/Logs/Lighting/cap_<Tag>.
$r=$Repo; $exe=$UnityExe
$env:LIGHT_CAPTURE_DIR="$r\unity\Logs\Lighting\cap_$Tag"; New-Item -ItemType Directory -Force $env:LIGHT_CAPTURE_DIR | Out-Null
$p=Start-Process $exe -ArgumentList @('-batchmode','-projectPath',"`"$r\unity`"",'-runTests','-testPlatform','PlayMode','-testFilter','LightingCaptureTools','-testResults',"`"$r\unity\Logs\cap_$Tag.xml`"",'-logFile',"`"$r\unity\Logs\cap_$Tag.log`"") -Wait -PassThru
$xml=[xml](Get-Content "$r\unity\Logs\cap_$Tag.xml"); $tr=$xml.'test-run'; "capture total=$($tr.total) passed=$($tr.passed) failed=$($tr.failed)"
(Get-ChildItem $env:LIGHT_CAPTURE_DIR -Filter *.png).Count.ToString() + " images"
