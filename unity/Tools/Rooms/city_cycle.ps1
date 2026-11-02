param([string]$Tag, [string]$Views = 'corridor_views_c.json', [switch]$NoApply, [switch]$NoBake)
# Corridor + city cycle: Apply corridor -> build city -> sky/haze -> connected bake -> reflections -> corridor captures. One Unity at a time.
$s='C:\Users\RUHAN\AppData\Local\Temp\claude\c--Users-RUHAN-Downloads-procsense-fyp-main\5f6e9808-39a5-4600-a178-679c9fcf001e\scratchpad\unity_run.ps1'
$u='C:\Users\RUHAN\Downloads\procsense-fyp-main\procsense-fyp-main\unity'
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
