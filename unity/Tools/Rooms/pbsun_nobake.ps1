param([string]$Tag)
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
# Patient bay sunlight (golden.json sun) -> bake -> reflections -> stills + videos with the desktop sunshafts and dust.
$s=(Join-Path $PSScriptRoot 'unity_run.ps1')
$u=(Join-Path $Repo 'unity')
function Step($label, $method, $pat) { "== $label"; & $s -Method $method -Log "pbs_${label}_$Tag" | Select-String "error CS|Exception|$pat" | Select-Object -First 6 }
Step 'beams' 'ProcSense.EditorTools.PatientBaySun.RemoveBeams' '^\[PatientBaySun\]'
Step 'shaftsoff' 'ProcSense.EditorTools.PatientBaySun.Shafts' '^\[PatientBaySun\]'
$rc = Join-Path $u 'Tools\Rooms\room_capture.ps1'; $rv = Join-Path $u 'Tools\Rooms\room_video.ps1'
& $rc -Scene PatientBay -Views (Join-Path $u 'Tools\Rooms\pbsun_views.json') -Tag "${Tag}s" -Width 1400
& $rv -Scene PatientBay -Views Tools\Rooms\pbvid_a.json -Tag "${Tag}A"
& $rv -Scene PatientBay -Views Tools\Rooms\pbvid_b.json -Tag "${Tag}B" -Frames 60
"pbsun_cycle done"
