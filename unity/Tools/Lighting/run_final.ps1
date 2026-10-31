param([string]$Tag, [switch]$Final, [switch]$NoRefl, [switch]$Room, [switch]$NoBake)
# Final-look cycle: [-Room FinalLookRoom.Apply after GoldenHourLighting.Apply] -> connected bake -> reflections -> PlayMode captures.
$s='C:\Users\RUHAN\AppData\Local\Temp\claude\c--Users-RUHAN-Downloads-procsense-fyp-main\5f6e9808-39a5-4600-a178-679c9fcf001e\scratchpad\unity_run.ps1'
$r='C:\Users\RUHAN\Downloads\procsense-fyp-main\procsense-fyp-main'; $exe='C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe'
"== apply"; & $s -Method ProcSense.EditorTools.GoldenHourLighting.Apply -Log "fl_apply_$Tag" | Select-String 'error CS|Exception|\[GoldenHour\]' | Select-Object -First 3
if ($Room) { "== room"; & $s -Method ProcSense.EditorTools.FinalLookRoom.Apply -Log "fl_room_$Tag" | Select-String 'error CS|Exception|\[FinalLookRoom\]' | Select-Object -First 4 }
$extra = @(); if ($Final) { $extra = @('-final') }
if (-not $NoBake) { "== bake"; & $s -Method ProcSense.EditorTools.GoldenHourLighting.Bake -Log "fl_bake_$Tag" -Extra $extra | Select-String 'error CS|Exception|\[GoldenHour\]' | Select-Object -First 3 }
if (-not $NoRefl) { "== reflections"; & $s -Method ProcSense.EditorTools.GoldenHourLighting.Reflections -Log "fl_refl_$Tag" | Select-String 'error CS|Exception|\[GoldenHour\]' | Select-Object -First 3 }
"== captures"; $env:LIGHT_CAPTURE_DIR="$r\unity\Logs\Lighting\cap_$Tag"
$p=Start-Process $exe -ArgumentList @('-batchmode','-projectPath',"`"$r\unity`"",'-runTests','-testPlatform','PlayMode','-testFilter','LightingCaptureTools','-testResults',"`"$r\unity\Logs\fl_cap_$Tag.xml`"",'-logFile',"`"$r\unity\Logs\fl_cap_$Tag.log`"") -Wait -PassThru
$x=[xml](Get-Content "$r\unity\Logs\fl_cap_$Tag.xml"); $tr=$x.'test-run'; "capture total=$($tr.total) passed=$($tr.passed) failed=$($tr.failed)"
