param([string]$Tag)
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
$s=(Join-Path $PSScriptRoot 'unity_run.ps1')
$u=(Join-Path $Repo 'unity')
function Step($label, $method, $pat, $extra=@()) { "== $label"; & $s -Method $method -Log "pd_${label}_$Tag" -Extra $extra | Select-String "error CS|Exception|$pat" | Select-Object -First 6 }
Step 'furnplace' 'ProcSense.EditorTools.WaitingFurniture.Place' '^\[WaitingFurniture\]'
Step 'decor' 'ProcSense.EditorTools.WallDecor.Apply' '^\[WallDecor\] (Corridor|applied)'
Step 'people' 'ProcSense.EditorTools.CorridorPeople.Apply' '^\[CorridorPeople\]'
Step 'ots' 'ProcSense.EditorTools.OtContactShadows.Apply' '^\[OtContactShadows\] applied'
"placement done"
