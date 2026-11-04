param([string]$Tag)
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
# 3 Nov 2026: retargeted animated characters -> import -> bench seat anchors -> people -> contact shadows -> captures (no bake; characters are probe lit).
$s=(Join-Path $PSScriptRoot 'unity_run.ps1')
$u=(Join-Path $Repo 'unity')
function Step($label, $method, $pat, $extra=@()) { "== $label"; & $s -Method $method -Log "an_${label}_$Tag" -Extra $extra | Select-String "error CS|Exception|$pat" | Select-Object -First 8 }
Step 'staffimport' 'ProcSense.EditorTools.StaffSetup.Import' '^\[StaffSetup\]'
Step 'staffprefabs' 'ProcSense.EditorTools.StaffSetup.BuildPrefabs' '^\[StaffSetup\]'
Step 'staffcloth' 'ProcSense.EditorTools.StaffSetup.FixCloth' '^\[StaffSetup\]'
Step 'furnplace' 'ProcSense.EditorTools.WaitingFurniture.Place' '^\[WaitingFurniture\] placed'
Step 'people' 'ProcSense.EditorTools.CorridorPeople.Apply' '^\[CorridorPeople\]'
Step 'cshadow' 'ProcSense.EditorTools.OtContactShadows.ApplyCorridor' '^\[OtContactShadows\]'
$rc = Join-Path $u 'Tools\Rooms\room_capture.ps1'
"== capture"; & $rc -Scene Corridor -Views (Join-Path $u 'Tools\Rooms\people_views.json') -Tag "${Tag}p" -Width 1400
& $rc -Scene OT -Views (Join-Path $u 'Tools\Rooms\ot_close.json') -Tag "${Tag}c" -Width 1400
"anim_cycle done"
