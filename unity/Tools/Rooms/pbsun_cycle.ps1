param([string]$Tag)
# Patient bay sunlight (golden.json sun) -> bake -> reflections -> stills + videos with the desktop sunshafts and dust.
$s='C:\Users\RUHAN\AppData\Local\Temp\claude\c--Users-RUHAN-Downloads-procsense-fyp-main\5f6e9808-39a5-4600-a178-679c9fcf001e\scratchpad\unity_run.ps1'
$u='C:\Users\RUHAN\Downloads\procsense-fyp-main\procsense-fyp-main\unity'
function Step($label, $method, $pat) { "== $label"; & $s -Method $method -Log "pbs_${label}_$Tag" | Select-String "error CS|Exception|$pat" | Select-Object -First 6 }
Step 'golden' 'ProcSense.EditorTools.GoldenHourLighting.Apply' '^\[GoldenHour\] applied'
Step 'plant' 'ProcSense.EditorTools.PatientBaySun.MovePlant' '^\[PatientBaySun\]'
Step 'beams' 'ProcSense.EditorTools.PatientBaySun.RemoveBeams' '^\[PatientBaySun\]'
Step 'shaftsoff' 'ProcSense.EditorTools.PatientBaySun.Shafts' '^\[PatientBaySun\]'
Step 'bake' 'ProcSense.EditorTools.GoldenHourLighting.Bake' '^\[GoldenHour\]'
Step 'refl' 'ProcSense.EditorTools.GoldenHourLighting.Reflections' '^\[GoldenHour\]'
$rc = Join-Path $u 'Tools\Rooms\room_capture.ps1'; $rv = Join-Path $u 'Tools\Rooms\room_video.ps1'
& $rc -Scene PatientBay -Views (Join-Path $u 'Tools\Rooms\pbsun_views.json') -Tag "${Tag}s" -Width 1400
& $rv -Scene PatientBay -Views Tools\Rooms\pbvid_a.json -Tag "${Tag}A"
& $rv -Scene PatientBay -Views Tools\Rooms\pbvid_b.json -Tag "${Tag}B" -Frames 60
"pbsun_cycle done"
