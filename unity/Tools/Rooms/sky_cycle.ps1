param([string]$Tag, [string]$Rot="0", [string]$Exp="1.0")
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
$s=(Join-Path $PSScriptRoot 'unity_run.ps1')
$u=(Join-Path $Repo 'unity')
$env:SKY_ROTATION=$Rot; $env:SKY_EXPOSURE=$Exp
"== photosky"; & $s -Method 'ProcSense.EditorTools.CityExterior.CalibratePhoto' -Log "sky_${Tag}" | Select-String "error CS|Exception|^\[CityExterior\]" | Select-Object -First 6
$rc = Join-Path $u 'Tools\Rooms\room_capture.ps1'
& $rc -Scene Corridor -Views (Join-Path $u 'Tools\Rooms\corridor_views_c.json') -Tag $Tag
& $rc -Scene PatientBay -Views (Join-Path $u 'Tools\Rooms\pb_views.json') -Tag "${Tag}b" -Width 1400
"sky_cycle done"
