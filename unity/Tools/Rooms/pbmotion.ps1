param([string]$Tag)
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
# Patient on the bed: re-place him (centred on the mattress), add the motion script, capture stills and a 6 s clip (one to two breaths, a blink).
$s=(Join-Path $PSScriptRoot 'unity_run.ps1')
$s=(Join-Path $PSScriptRoot 'unity_run.ps1')
$u=(Join-Path $Repo 'unity')
"== patient"; & $s -Method 'ProcSense.EditorTools.PatientBayPatient.Apply' -Log "pbpat_$Tag" | Select-String "error CS|Exception" | Select-Object -First 8
& (Join-Path $u 'Tools\Rooms\room_capture.ps1') -Scene PatientBay -Views (Join-Path $u 'Tools\Rooms\pbpatient_views.json') -Tag "${Tag}" -Width 1400
& (Join-Path $u 'Tools\Rooms\room_video.ps1') -Scene PatientBay -Views Tools\Rooms\pbbreath.json -Tag "${Tag}v" -Frames 72
"pbmotion done0"
& (Join-Path $u 'Tools\Rooms\room_video.ps1') -Scene PatientBay -Views Tools\Rooms\pbbreath_fixed.json -Tag "${Tag}f" -Frames 72
"pbmotion fixed done"
