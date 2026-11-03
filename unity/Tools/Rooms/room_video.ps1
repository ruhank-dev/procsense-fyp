param([string]$Scene, [string]$Views, [string]$Tag, [int]$Width = 1280, [int]$Frames = 72)
# Short video of a slow camera move (two views in the JSON) at a fixed 1/12 s game time step so particles and slow noise animate:  room_video.ps1 -Scene Corridor -Views <json> -Tag v1
# Writes unity/Logs/Rooms/<Scene>_<Tag>/f0000.png ... and <Scene>_<Tag>.mp4 (ffmpeg from the winget package).
$r='C:\Users\RUHAN\Downloads\procsense-fyp-main\procsense-fyp-main'; $exe='C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe'
$env:ROOM_SCENE=$Scene; $env:ROOM_VIEWS=$Views; $env:ROOM_W="$Width"; $env:ROOM_FRAMES="$Frames"; $env:ROOM_CAPTURE_DIR="$r\unity\Logs\Rooms\${Scene}_$Tag"; New-Item -ItemType Directory -Force $env:ROOM_CAPTURE_DIR | Out-Null
$p=Start-Process $exe -ArgumentList @('-batchmode','-projectPath',"`"$r\unity`"",'-runTests','-testPlatform','PlayMode','-testFilter','Capture_room_video','-testResults',"`"$r\unity\Logs\video_${Scene}_$Tag.xml`"",'-logFile',"`"$r\unity\Logs\video_${Scene}_$Tag.log`"") -PassThru -Wait -NoNewWindow
$xml=[xml](Get-Content "$r\unity\Logs\video_${Scene}_$Tag.xml"); $tr=$xml.'test-run'; "video total=$($tr.total) passed=$($tr.passed) failed=$($tr.failed)"
$ff='C:\Users\RUHAN\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-9.0.2-full_build\bin\ffmpeg.exe'
& $ff -y -loglevel error -framerate 12 -i "$env:ROOM_CAPTURE_DIR\f%04d.png" -c:v libx264 -pix_fmt yuv420p -crf 20 "$env:ROOM_CAPTURE_DIR\${Scene}_$Tag.mp4"
(Get-ChildItem $env:ROOM_CAPTURE_DIR -Filter *.png).Count.ToString() + " frames, video: $env:ROOM_CAPTURE_DIR\${Scene}_$Tag.mp4"
