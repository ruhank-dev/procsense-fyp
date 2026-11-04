param([string]$Tag = 'cmp')
# Fixed-camera stills of the three rooms from THIS checkout (use the same script from the working project and from a fresh clone, then compare with tools\setup\compare_captures.py):
# PatientBay (5 views: pbsun_views.json), Corridor (7 views: light_views.json), OT (ot_views.json). Output: unity\Logs\Rooms\<Scene>_<Tag>\*.png
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$u = Join-Path $Repo 'unity'
& (Join-Path $PSScriptRoot 'room_capture.ps1') -Scene PatientBay -Views (Join-Path $u 'Tools\Rooms\pbsun_views.json') -Tag $Tag -Width 1400 | Select-Object -Last 1
& (Join-Path $PSScriptRoot 'room_capture.ps1') -Scene Corridor -Views (Join-Path $u 'Tools\Rooms\light_views.json') -Tag $Tag -Width 1400 | Select-Object -Last 1
& (Join-Path $PSScriptRoot 'room_capture.ps1') -Scene OT -Views (Join-Path $u 'Tools\Rooms\ot_views.json') -Tag $Tag -Width 1400 | Select-Object -Last 1
"env_captures done ($Repo)"
