param([string]$Tag)
# Patient on the bed: re-place him (centred on the mattress), add the motion script, capture stills and a 6 s clip (one to two breaths, a blink).
$s='C:\Users\RUHAN\AppData\Local\Temp\claude\c--Users-RUHAN-Downloads-procsense-fyp-main\5f6e9808-39a5-4600-a178-679c9fcf001e\scratchpad\unity_run.ps1'
$s='C:\Users\RUHAN\AppData\Local\Temp\claude\c--Users-RUHAN-Downloads-procsense-fyp-main\5f6e9808-39a5-4600-a178-679c9fcf001e\scratchpad\unity_run.ps1'
$u='C:\Users\RUHAN\Downloads\procsense-fyp-main\procsense-fyp-main\unity'
"== patient"; & $s -Method 'ProcSense.EditorTools.PatientBayPatient.Apply' -Log "pbpat_$Tag" | Select-String "error CS|Exception" | Select-Object -First 8
& (Join-Path $u 'Tools\Rooms\room_capture.ps1') -Scene PatientBay -Views (Join-Path $u 'Tools\Rooms\pbpatient_views.json') -Tag "${Tag}" -Width 1400
& (Join-Path $u 'Tools\Rooms\room_video.ps1') -Scene PatientBay -Views Tools\Rooms\pbbreath.json -Tag "${Tag}v" -Frames 72
"pbmotion done0"
& (Join-Path $u 'Tools\Rooms\room_video.ps1') -Scene PatientBay -Views Tools\Rooms\pbbreath_fixed.json -Tag "${Tag}f" -Frames 72
"pbmotion fixed done"
