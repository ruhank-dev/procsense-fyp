param([string]$Tag, [string]$Rot="0", [string]$Exp="1.0")
$s='C:\Users\RUHAN\AppData\Local\Temp\claude\c--Users-RUHAN-Downloads-procsense-fyp-main\5f6e9808-39a5-4600-a178-679c9fcf001e\scratchpad\unity_run.ps1'
$u='C:\Users\RUHAN\Downloads\procsense-fyp-main\procsense-fyp-main\unity'
$env:SKY_ROTATION=$Rot; $env:SKY_EXPOSURE=$Exp
"== photosky"; & $s -Method 'ProcSense.EditorTools.CityExterior.CalibratePhoto' -Log "sky_${Tag}" | Select-String "error CS|Exception|^\[CityExterior\]" | Select-Object -First 6
$rc = Join-Path $u 'Tools\Rooms\room_capture.ps1'
& $rc -Scene Corridor -Views (Join-Path $u 'Tools\Rooms\corridor_views_c.json') -Tag $Tag
& $rc -Scene PatientBay -Views (Join-Path $u 'Tools\Rooms\pb_views.json') -Tag "${Tag}b" -Width 1400
"sky_cycle done"
