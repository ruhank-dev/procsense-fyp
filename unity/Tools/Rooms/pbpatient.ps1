param([string]$Tag)
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
$s=(Join-Path $PSScriptRoot 'unity_run.ps1')
$u=(Join-Path $Repo 'unity')
"== patient"; & $s -Method 'ProcSense.EditorTools.PatientBayPatient.Apply' -Log "pbpat_$Tag" | Select-String "error CS|Exception|PatientBayPatient" | Select-Object -First 8
& (Join-Path $u 'Tools\Rooms\room_capture.ps1') -Scene PatientBay -Views (Join-Path $u 'Tools\Rooms\pbpatient_views.json') -Tag "${Tag}" -Width 1400
"pbpatient done"
