param([string]$Tag)
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
$s=(Join-Path $PSScriptRoot 'unity_run.ps1')
$u=(Join-Path $Repo 'unity')
"== materials"; & $s -Method 'ProcSense.EditorTools.MaterialOverhaul.Apply' -Log "mat_${Tag}" | Select-String "error CS|Exception|^\[MaterialOverhaul\] applied" | Select-Object -First 4
"== ot equipment"; & $s -Method 'ProcSense.EditorTools.OtEquipmentUpgrade.Apply' -Log "ote_${Tag}" | Select-String "error CS|Exception|^\[OtEquipmentUpgrade\]" | Select-Object -First 10
& (Join-Path $u 'Tools\Rooms\room_capture.ps1') -Scene OT -Views (Join-Path $u 'Tools\Rooms\ot_views.json') -Tag "${Tag}o" -Width 1400
"ot_cycle done"
