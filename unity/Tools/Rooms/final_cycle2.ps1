param([string]$Tag)
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
# 3 Nov 2026 (second request): props held properly, clocks/frames with art, sun direction + intensity, wall sconces, light shafts, people, glass -> final bake -> captures.
$s=(Join-Path $PSScriptRoot 'unity_run.ps1')
$u=(Join-Path $Repo 'unity')
function Step($label, $method, $pat, $extra=@()) { "== $label"; & $s -Method $method -Log "fc2_${label}_$Tag" -Extra $extra | Select-String "error CS|Exception|$pat" | Select-Object -First 6 }
Step 'staffimport' 'ProcSense.EditorTools.StaffSetup.Import' '^\[StaffSetup\]'
Step 'staffprefabs' 'ProcSense.EditorTools.StaffSetup.BuildPrefabs' '^\[StaffSetup\]'
Step 'staffcloth' 'ProcSense.EditorTools.StaffSetup.FixCloth' '^\[StaffSetup\]'
Step 'mat' 'ProcSense.EditorTools.MaterialOverhaul.Apply' '^\[MaterialOverhaul\] applied'
Step 'phprops' 'ProcSense.EditorTools.PolyHavenProps.Import' '^\[PolyHaven\]'
Step 'furnplace' 'ProcSense.EditorTools.WaitingFurniture.Place' '^\[WaitingFurniture\]'
Step 'decor' 'ProcSense.EditorTools.WallDecor.Apply' '^\[WallDecor\] (Corridor|applied)'
Step 'lighting' 'ProcSense.EditorTools.WallLighting.Apply' '^\[WallLighting\]'
Step 'people' 'ProcSense.EditorTools.CorridorPeople.Apply' '^\[CorridorPeople\]'
Step 'cshadow' 'ProcSense.EditorTools.OtContactShadows.ApplyCorridor' '^\[OtContactShadows\]'
Step 'ots' 'ProcSense.EditorTools.OtContactShadows.Apply' '^\[OtContactShadows\] applied'
Step 'golden' 'ProcSense.EditorTools.GoldenHourLighting.Apply' '^\[GoldenHour\] applied'
Step 'bake' 'ProcSense.EditorTools.GoldenHourLighting.Bake' '^\[GoldenHour\]' @('-final')
Step 'refl' 'ProcSense.EditorTools.GoldenHourLighting.Reflections' '^\[GoldenHour\]'
Step 'manifests' 'ProcSense.Editor.Rooms.RoomManifests.WriteAll' 'anifest'
$rc = Join-Path $u 'Tools\Rooms\room_capture.ps1'
"== capture"; & $rc -Scene Corridor -Views (Join-Path $u 'Tools\Rooms\corridor_views_c.json') -Tag $Tag
& $rc -Scene Corridor -Views (Join-Path $u 'Tools\Rooms\people_views.json') -Tag "${Tag}p" -Width 1400
& $rc -Scene PatientBay -Views (Join-Path $u 'Tools\Rooms\pb_views.json') -Tag "${Tag}b" -Width 1400
& $rc -Scene OT -Views (Join-Path $u 'Tools\Rooms\ot_views.json') -Tag "${Tag}o" -Width 1400
& $rc -Scene OT -Views (Join-Path $u 'Tools\Rooms\ot_close.json') -Tag "${Tag}c" -Width 1400
"final_cycle2 done"
