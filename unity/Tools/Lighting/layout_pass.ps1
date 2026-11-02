param([string]$Tag)
# Layout pass for the hospital overhaul (PatientBay): curtain rig off -> import new models -> rig again -> apply layout. One Unity at a time.
$s='C:\Users\RUHAN\AppData\Local\Temp\claude\c--Users-RUHAN-Downloads-procsense-fyp-main\5f6e9808-39a5-4600-a178-679c9fcf001e\scratchpad\unity_run.ps1'
function Step($label, $method, $pat) { "== $label"; & $s -Method $method -Log "lp_${label}_$Tag" | Select-String "error CS|Exception|$pat" | Select-Object -First 5 }
Step 'unrig' 'ProcSense.EditorTools.CurtainRigBuilder.Remove' '\[CurtainRig\]'
Step 'import' 'ProcSense.EditorTools.PatientBayLayout.Import' '\[PatientBayLayout\]'
Step 'rig' 'ProcSense.EditorTools.CurtainRigBuilder.Build' '\[CurtainRig\]'
Step 'apply' 'ProcSense.EditorTools.PatientBayLayout.Apply' '\[PatientBayLayout\]'
"layout done"
