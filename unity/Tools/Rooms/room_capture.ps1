param([string]$Scene, [string]$Views, [string]$Tag, [int]$Width = 1600)
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
# Capture the views in a JSON file for one room: room_capture.ps1 -Scene Corridor -Views <json> -Tag c0  ->  unity/Logs/Rooms/<Scene>_<Tag>/*.png
$r=$Repo; $exe=$UnityExe
$env:ROOM_SCENE=$Scene; $env:ROOM_VIEWS=$Views; $env:ROOM_W="$Width"; $env:ROOM_CAPTURE_DIR="$r\unity\Logs\Rooms\${Scene}_$Tag"; New-Item -ItemType Directory -Force $env:ROOM_CAPTURE_DIR | Out-Null
$p=Start-Process $exe -ArgumentList @('-batchmode','-projectPath',"`"$r\unity`"",'-runTests','-testPlatform','PlayMode','-testFilter','RoomCaptureTools','-testResults',"`"$r\unity\Logs\room_${Scene}_$Tag.xml`"",'-logFile',"`"$r\unity\Logs\room_${Scene}_$Tag.log`"") -Wait -PassThru
$xml=[xml](Get-Content "$r\unity\Logs\room_${Scene}_$Tag.xml"); $tr=$xml.'test-run'; "capture total=$($tr.total) passed=$($tr.passed) failed=$($tr.failed)"
(Get-ChildItem $env:ROOM_CAPTURE_DIR -Filter *.png).Count.ToString() + " images in $env:ROOM_CAPTURE_DIR"
