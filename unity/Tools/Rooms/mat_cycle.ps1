param([string]$Tag)
$s='C:\Users\RUHAN\AppData\Local\Temp\claude\c--Users-RUHAN-Downloads-procsense-fyp-main\5f6e9808-39a5-4600-a178-679c9fcf001e\scratchpad\unity_run.ps1'
$u='C:\Users\RUHAN\Downloads\procsense-fyp-main\procsense-fyp-main\unity'
"== materials"; & $s -Method 'ProcSense.EditorTools.MaterialOverhaul.Apply' -Log "mat_${Tag}" | Select-String "error CS|Exception|^\[MaterialOverhaul\]" | Select-Object -First 6
$rc = Join-Path $u 'Tools\Rooms\room_capture.ps1'
& $rc -Scene Corridor -Views (Join-Path $u 'Tools\Rooms\corridor_views_c.json') -Tag $Tag
& $rc -Scene PatientBay -Views (Join-Path $u 'Tools\Rooms\pb_views.json') -Tag "${Tag}b" -Width 1400
& $rc -Scene OT -Views (Join-Path $u 'Tools\Rooms\ot_views.json') -Tag "${Tag}o" -Width 1400
"mat_cycle done"
