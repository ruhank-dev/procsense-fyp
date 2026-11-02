param([string]$Tag)
$s='C:\Users\RUHAN\AppData\Local\Temp\claude\c--Users-RUHAN-Downloads-procsense-fyp-main\5f6e9808-39a5-4600-a178-679c9fcf001e\scratchpad\unity_run.ps1'
$u='C:\Users\RUHAN\Downloads\procsense-fyp-main\procsense-fyp-main\unity'
function Step($label, $method, $pat, $extra=@()) { "== $label"; & $s -Method $method -Log "pd_${label}_$Tag" -Extra $extra | Select-String "error CS|Exception|$pat" | Select-Object -First 6 }
Step 'furnplace' 'ProcSense.EditorTools.WaitingFurniture.Place' '^\[WaitingFurniture\]'
Step 'decor' 'ProcSense.EditorTools.WallDecor.Apply' '^\[WallDecor\] (Corridor|applied)'
Step 'people' 'ProcSense.EditorTools.CorridorPeople.Apply' '^\[CorridorPeople\]'
Step 'ots' 'ProcSense.EditorTools.OtContactShadows.Apply' '^\[OtContactShadows\] applied'
"placement done"
