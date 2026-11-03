param([string]$Tag, [string]$Views = 'corridor_views_c.json', [switch]$NoApply, [switch]$NoBake)
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
# Corridor + city cycle: Apply corridor -> build city -> sky/haze -> connected bake -> reflections -> corridor captures. One Unity at a time.
$s=(Join-Path $PSScriptRoot 'unity_run.ps1')
$u=(Join-Path $Repo 'unity')
function Step($label, $method, $pat) { "== $label"; & $s -Method $method -Log "cc_${label}_$Tag" | Select-String "error CS|Exception|$pat" | Select-Object -First 4 }
if (-not $NoApply) {
  Step 'apply' 'ProcSense.EditorTools.CorridorOverhaul.Apply' '\[CorridorOverhaul\]'
  Step 'city' 'ProcSense.EditorTools.CityExterior.Build' '\[CityExterior\]'
  Step 'sky' 'ProcSense.EditorTools.CityExterior.Calibrate' '\[CityExterior\]'
}
if (-not $NoBake) {
  Step 'bake' 'ProcSense.EditorTools.GoldenHourLighting.Bake' '\[GoldenHour\]'
  Step 'refl' 'ProcSense.EditorTools.GoldenHourLighting.Reflections' '\[GoldenHour\]'
}
"== capture"; & "$u\Tools\Rooms\room_capture.ps1" -Scene Corridor -Views "$u\Tools\Rooms\$Views" -Tag $Tag
"cycle done"
