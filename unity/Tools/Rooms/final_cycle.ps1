param([string]$Tag)
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
# Final geometry cycle for the expanded corridor: apply -> city -> sky -> connected bake -> reflections -> room manifests -> transport navmesh -> 13 corridor captures. One Unity at a time.
$s=(Join-Path $PSScriptRoot 'unity_run.ps1')
$u=(Join-Path $Repo 'unity')
function Step($label, $method, $pat) { "== $label"; & $s -Method $method -Log "fc_${label}_$Tag" | Select-String "error CS|Exception|$pat" | Select-Object -First 4 }
Step 'apply' 'ProcSense.EditorTools.CorridorOverhaul.Apply' '\[CorridorOverhaul\]'
Step 'city' 'ProcSense.EditorTools.CityExterior.Build' '\[CityExterior\] built'
Step 'sky' 'ProcSense.EditorTools.CityExterior.Calibrate' '\[CityExterior\]'
Step 'bake' 'ProcSense.EditorTools.GoldenHourLighting.Bake' '\[GoldenHour\]'
Step 'refl' 'ProcSense.EditorTools.GoldenHourLighting.Reflections' '\[GoldenHour\]'
Step 'manifests' 'ProcSense.Editor.Rooms.RoomManifests.WriteAll' 'anifest'
Step 'navmesh' 'ProcSense.EditorTools.TransportSetup.Apply' 'ransport'
"== capture"; & "$u\Tools\Rooms\room_capture.ps1" -Scene Corridor -Views "$u\Tools\Rooms\corridor_views_c.json" -Tag $Tag
"final cycle done"
