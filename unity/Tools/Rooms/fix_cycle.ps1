param([string]$Tag)
# 3 Nov 2026 QA fix cycle (no bake): staff re-import, materials, OT equipment + contact shadows, bay contact-shadow refresh, staff captures.
$s='C:\Users\RUHAN\AppData\Local\Temp\claude\c--Users-RUHAN-Downloads-procsense-fyp-main\5f6e9808-39a5-4600-a178-679c9fcf001e\scratchpad\unity_run.ps1'
$u='C:\Users\RUHAN\Downloads\procsense-fyp-main\procsense-fyp-main\unity'
function Step($label, $method, $pat) { "== $label"; & $s -Method $method -Log "fx_${label}_$Tag" | Select-String "error CS|Exception|$pat" | Select-Object -First 4 }
Step 'staffimport' 'ProcSense.EditorTools.StaffSetup.Import' '^\[StaffSetup\]'
Step 'staffprefabs' 'ProcSense.EditorTools.StaffSetup.BuildPrefabs' '^\[StaffSetup\]'
Step 'staffcloth' 'ProcSense.EditorTools.StaffSetup.FixCloth' '^\[StaffSetup\]'
Step 'mat' 'ProcSense.EditorTools.MaterialOverhaul.Apply' '^\[MaterialOverhaul\] applied'
Step 'ote' 'ProcSense.EditorTools.OtEquipmentUpgrade.Apply' '^\[OtEquipmentUpgrade\] applied'
Step 'otshadow' 'ProcSense.EditorTools.OtContactShadows.Apply' '^\[OtContactShadows\]'
Step 'bayshadow' 'ProcSense.EditorTools.PatientBayLookUpgrade.RefreshContactShadows' 'LookUpgrade'
$rc = Join-Path $u 'Tools\Rooms\room_capture.ps1'
& $rc -Scene Corridor -Views (Join-Path $u 'Tools\Rooms\staff_views.json') -Tag "${Tag}s" -Width 1400
& $rc -Scene PatientBay -Views (Join-Path $u 'Tools\Rooms\pb_views.json') -Tag "${Tag}b" -Width 1400
"fix_cycle done"
