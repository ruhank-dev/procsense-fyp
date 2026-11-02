param([string]$Tag)
# 3 Nov 2026: materials + OT equipment -> final connected bake -> reflections -> manifests -> captures (corridor, staff, bay, OT overview + OT close-ups). One Unity at a time.
$s='C:\Users\RUHAN\AppData\Local\Temp\claude\c--Users-RUHAN-Downloads-procsense-fyp-main\5f6e9808-39a5-4600-a178-679c9fcf001e\scratchpad\unity_run.ps1'
$u='C:\Users\RUHAN\Downloads\procsense-fyp-main\procsense-fyp-main\unity'
function Step($label, $method, $pat, $extra=@()) { "== $label"; & $s -Method $method -Log "bc_${label}_$Tag" -Extra $extra | Select-String "error CS|Exception|$pat" | Select-Object -First 4 }
Step 'mat' 'ProcSense.EditorTools.MaterialOverhaul.Apply' '^\[MaterialOverhaul\] applied'
Step 'ote' 'ProcSense.EditorTools.OtEquipmentUpgrade.Apply' '^\[OtEquipmentUpgrade\] applied'
Step 'bake' 'ProcSense.EditorTools.GoldenHourLighting.Bake' '^\[GoldenHour\]' @('-final')
Step 'refl' 'ProcSense.EditorTools.GoldenHourLighting.Reflections' '^\[GoldenHour\]'
Step 'manifests' 'ProcSense.Editor.Rooms.RoomManifests.WriteAll' 'anifest'
$rc = Join-Path $u 'Tools\Rooms\room_capture.ps1'
"== capture"; & $rc -Scene Corridor -Views (Join-Path $u 'Tools\Rooms\corridor_views_c.json') -Tag $Tag
& $rc -Scene Corridor -Views (Join-Path $u 'Tools\Rooms\staff_views.json') -Tag "${Tag}s" -Width 1400
& $rc -Scene PatientBay -Views (Join-Path $u 'Tools\Rooms\pb_views.json') -Tag "${Tag}b" -Width 1400
& $rc -Scene OT -Views (Join-Path $u 'Tools\Rooms\ot_views.json') -Tag "${Tag}o" -Width 1400
& $rc -Scene OT -Views (Join-Path $u 'Tools\Rooms\ot_close.json') -Tag "${Tag}c" -Width 1400
"bake_cycle done"
