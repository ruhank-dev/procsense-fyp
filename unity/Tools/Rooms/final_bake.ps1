param([string]$Tag = 'fb')
# Final-quality bake of the three connected rooms from the tracked sun config (unity\Tools\Lighting\golden.json): rename migration, apply the bay sun, bake with -final (about 45 minutes), refresh reflections, then re-enable the bay shafts.
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$s = (Join-Path $PSScriptRoot 'unity_run.ps1')
function Step($label, $method, $extra = @()) { "== $label"; & $s -Method $method -Log "fb_${label}_$Tag" -Extra $extra | Select-Object -Last 4 }
Step 'rename' 'ProcSense.EditorTools.CorridorSun.RenameSunroom'
Step 'golden' 'ProcSense.EditorTools.GoldenHourLighting.Apply'
Step 'bake' 'ProcSense.EditorTools.GoldenHourLighting.Bake' @('-final')
Step 'refl' 'ProcSense.EditorTools.GoldenHourLighting.Reflections'
Step 'bayshafts' 'ProcSense.EditorTools.PatientBaySun.Shafts'
"final_bake done"
