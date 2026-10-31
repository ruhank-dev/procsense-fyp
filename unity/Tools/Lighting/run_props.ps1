param([string]$Tag)
$s='C:\Users\RUHAN\AppData\Local\Temp\claude\c--Users-RUHAN-Downloads-procsense-fyp-main\5f6e9808-39a5-4600-a178-679c9fcf001e\scratchpad\unity_run.ps1'
$r='C:\Users\RUHAN\Downloads\procsense-fyp-main\procsense-fyp-main'; $exe='C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe'
"== apply"; & $s -Method ProcSense.EditorTools.GoldenHourLighting.Apply -Log "fp_apply_$Tag" | Select-String 'error CS|Exception|\[GoldenHour\]' | Select-Object -First 3
"== room"; & $s -Method ProcSense.EditorTools.FinalLookRoom.Apply -Log "fp_room_$Tag" | Select-String 'error CS|Exception|\[FinalLookRoom\]' | Select-Object -First 4
"== import"; & $s -Method ProcSense.EditorTools.FinalLookProps.Import -Log "fp_import_$Tag" | Select-String 'error CS|Exception|\[FinalLookProps\]' | Select-Object -First 6
"== place"; & $s -Method ProcSense.EditorTools.FinalLookProps.Place -Log "fp_place_$Tag" | Select-String 'error CS|Exception|\[FinalLookProps\]' | Select-Object -First 6
"== captures"; $env:LIGHT_CAPTURE_DIR="$r\unity\Logs\Lighting\cap_$Tag"
$p=Start-Process $exe -ArgumentList @('-batchmode','-projectPath',"`"$r\unity`"",'-runTests','-testPlatform','PlayMode','-testFilter','LightingCaptureTools','-testResults',"`"$r\unity\Logs\fp_cap_$Tag.xml`"",'-logFile',"`"$r\unity\Logs\fp_cap_$Tag.log`"") -Wait -PassThru
$x=[xml](Get-Content "$r\unity\Logs\fp_cap_$Tag.xml"); $tr=$x.'test-run'; "capture total=$($tr.total) passed=$($tr.passed) failed=$($tr.failed)"
