param([string]$Tag, [switch]$NoBake, [switch]$Final, [switch]$Staff)
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
# Second-generation cycle (1 Nov 2026 review): imports (city art/trees, doors, plants, [staff]) -> corridor apply -> city -> sky -> doors -> OT parity -> [bake -> reflections] -> corridor, staff, patient-bay and OT captures. One Unity at a time.
$s=(Join-Path $PSScriptRoot 'unity_run.ps1')
$u=(Join-Path $Repo 'unity')
function Step($label, $method, $pat, $extra=@()) { "== $label"; & $s -Method $method -Log "c2_${label}_$Tag" -Extra $extra | Select-String "error CS|Exception|$pat" | Select-Object -First 4 }
Step 'cityimport' 'ProcSense.EditorTools.CityExterior.Import' '^\[CityExterior\]'
Step 'doorimport' 'ProcSense.EditorTools.DoorUpgrade.Import' '^\[DoorUpgrade\]'
if ($Staff) {
  Step 'staffimport' 'ProcSense.EditorTools.StaffSetup.Import' '^\[StaffSetup\]'
  Step 'staffprefabs' 'ProcSense.EditorTools.StaffSetup.BuildPrefabs' '^\[StaffSetup\]'
  Step 'staffcloth' 'ProcSense.EditorTools.StaffSetup.FixCloth' '^\[StaffSetup\]'
}
Step 'apply' 'ProcSense.EditorTools.CorridorOverhaul.Apply' '^\[CorridorOverhaul\]'
Step 'city' 'ProcSense.EditorTools.CityExterior.Build' '^\[CityExterior\] built'
Step 'sky' 'ProcSense.EditorTools.CityExterior.Calibrate' '^\[CityExterior\]'
Step 'doors' 'ProcSense.EditorTools.DoorUpgrade.Apply' '^\[DoorUpgrade\]'
Step 'ot' 'ProcSense.EditorTools.OtParity.Apply' '^\[OtParity\]'
if (-not $NoBake) {
  $x=@(); if ($Final) { $x=@('-final') }
  Step 'bake' 'ProcSense.EditorTools.GoldenHourLighting.Bake' '^\[GoldenHour\]' $x
  Step 'refl' 'ProcSense.EditorTools.GoldenHourLighting.Reflections' '^\[GoldenHour\]'
}
$rc = Join-Path $u 'Tools\Rooms\room_capture.ps1'
"== capture corridor"; & $rc -Scene Corridor -Views (Join-Path $u 'Tools\Rooms\corridor_views_c.json') -Tag $Tag
"== capture staff"; & $rc -Scene Corridor -Views (Join-Path $u 'Tools\Rooms\staff_views.json') -Tag "${Tag}s" -Width 1400
"== capture bay"; & $rc -Scene PatientBay -Views (Join-Path $u 'Tools\Rooms\pb_gown_views.json') -Tag "${Tag}b" -Width 1400
"== capture ot"; & $rc -Scene OT -Views (Join-Path $u 'Tools\Rooms\ot_views.json') -Tag "${Tag}o" -Width 1400
"cycle2 done"
