param([string]$Tag)
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
# 3 Nov 2026 QA fix cycle (no bake): staff re-import, materials, OT equipment + contact shadows, bay contact-shadow refresh, staff captures.
$s=(Join-Path $PSScriptRoot 'unity_run.ps1')
$u=(Join-Path $Repo 'unity')
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
