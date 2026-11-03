param([string]$Tag)
# Layout pass for the hospital overhaul (PatientBay): curtain rig off -> import new models -> rig again -> apply layout. One Unity at a time.
$s=(Join-Path $PSScriptRoot 'unity_run.ps1')
function Step($label, $method, $pat) { "== $label"; & $s -Method $method -Log "lp_${label}_$Tag" | Select-String "error CS|Exception|$pat" | Select-Object -First 5 }
Step 'unrig' 'ProcSense.EditorTools.CurtainRigBuilder.Remove' '\[CurtainRig\]'
Step 'import' 'ProcSense.EditorTools.PatientBayLayout.Import' '\[PatientBayLayout\]'
Step 'rig' 'ProcSense.EditorTools.CurtainRigBuilder.Build' '\[CurtainRig\]'
Step 'apply' 'ProcSense.EditorTools.PatientBayLayout.Apply' '\[PatientBayLayout\]'
"layout done"
