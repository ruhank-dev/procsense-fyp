param([string]$Tag, [switch]$Final, [switch]$SkipDoors)
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
# 3 Nov 2026 (lighting reference): wider door vision panes -> corridor sun rig + lamps + exposure -> bake (preview, or -final) -> reflections -> captures of the corridor, patient bay and OT.
$s=(Join-Path $PSScriptRoot 'unity_run.ps1')
$u=(Join-Path $Repo 'unity')
function Step($label, $method, $pat, $extra=@()) { "== $label"; & $s -Method $method -Log "lc_${label}_$Tag" -Extra $extra | Select-String "error CS|Exception|$pat" | Select-Object -First 6 }
if (-not $SkipDoors) { Step 'doorimp' 'ProcSense.EditorTools.DoorUpgrade.Import' '^\[DoorUpgrade\]'; Step 'doorapply' 'ProcSense.EditorTools.DoorUpgrade.Apply' '^\[DoorUpgrade\]' }
Step 'sun' 'ProcSense.EditorTools.CorridorSun.Apply' '^\[CorridorSun\]'
Step 'roomsconces' 'ProcSense.EditorTools.CorridorSun.RoomSconces' '^\[CorridorSun\]'
Step 'golden' 'ProcSense.EditorTools.GoldenHourLighting.Apply' '^\[GoldenHour\] applied'
Step 'cshadow' 'ProcSense.EditorTools.OtContactShadows.ApplyCorridor' '^\[OtContactShadows\]'
$extra = @(); if ($Final) { $extra = @('-final') }
Step 'bake' 'ProcSense.EditorTools.GoldenHourLighting.Bake' '^\[GoldenHour\]' $extra
Step 'refl' 'ProcSense.EditorTools.GoldenHourLighting.Reflections' '^\[GoldenHour\]'
$rc = Join-Path $u 'Tools\Rooms\room_capture.ps1'
"== capture"
& $rc -Scene Corridor -Views (Join-Path $u 'Tools\Rooms\light_views.json') -Tag "${Tag}l" -Width 1400
& $rc -Scene PatientBay -Views (Join-Path $u 'Tools\Rooms\pb_views.json') -Tag "${Tag}b" -Width 1400
& $rc -Scene OT -Views (Join-Path $u 'Tools\Rooms\ot_views.json') -Tag "${Tag}o" -Width 1400
"== shafts on"
Step 'shafts' 'ProcSense.EditorTools.CorridorSun.ShaftsOn' '^\[CorridorSun\]'
& $rc -Scene Corridor -Views (Join-Path $u 'Tools\Rooms\light_views.json') -Tag "${Tag}v" -Width 1400
"light_cycle done"
