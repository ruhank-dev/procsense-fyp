param([string]$Tag)
$s='C:\Users\RUHAN\AppData\Local\Temp\claude\c--Users-RUHAN-Downloads-procsense-fyp-main\5f6e9808-39a5-4600-a178-679c9fcf001e\scratchpad\unity_run.ps1'
$u='C:\Users\RUHAN\Downloads\procsense-fyp-main\procsense-fyp-main\unity'
"== materials"; & $s -Method 'ProcSense.EditorTools.MaterialOverhaul.Apply' -Log "mat_${Tag}" | Select-String "error CS|Exception|^\[MaterialOverhaul\] applied" | Select-Object -First 4
"== ot equipment"; & $s -Method 'ProcSense.EditorTools.OtEquipmentUpgrade.Apply' -Log "ote_${Tag}" | Select-String "error CS|Exception|^\[OtEquipmentUpgrade\]" | Select-Object -First 10
& (Join-Path $u 'Tools\Rooms\room_capture.ps1') -Scene OT -Views (Join-Path $u 'Tools\Rooms\ot_views.json') -Tag "${Tag}o" -Width 1400
"ot_cycle done"
