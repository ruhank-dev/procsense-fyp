param([string]$Tag, [switch]$Final, [switch]$NoBake, [switch]$NoRefl, [switch]$Props, [switch]$Rigs, [switch]$Tests)
# Full final-look cycle in one Unity-at-a-time sequence: lights -> room -> [props import+place] -> materials -> bake -> reflections -> captures.
$s='C:\Users\RUHAN\AppData\Local\Temp\claude\c--Users-RUHAN-Downloads-procsense-fyp-main\5f6e9808-39a5-4600-a178-679c9fcf001e\scratchpad\unity_run.ps1'
$r='C:\Users\RUHAN\Downloads\procsense-fyp-main\procsense-fyp-main'; $exe='C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe'
function Step($label, $method, $log, $pat, $extra=@()) { "== $label"; & $s -Method $method -Log "fa_${log}_$Tag" -Extra $extra | Select-String "error CS|Exception|$pat" | Select-Object -First 4 }
Step 'apply' 'ProcSense.EditorTools.GoldenHourLighting.Apply' 'apply' '\[GoldenHour\]'
Step 'room' 'ProcSense.EditorTools.FinalLookRoom.Apply' 'room' '\[FinalLookRoom\]'
if ($Props) { Step 'import' 'ProcSense.EditorTools.FinalLookProps.Import' 'import' '\[FinalLookProps\]'; Step 'place' 'ProcSense.EditorTools.FinalLookProps.Place' 'place' '\[FinalLookProps\]' }
if ($Rigs) { Step 'speechshapes' 'ProcSense.EditorTools.PatientFaceSetup.AddSpeechShapes' 'sshape' '\[PatientFace\]'; Step 'face' 'ProcSense.EditorTools.PatientFaceSetup.Install' 'face' '\[PatientFace\]'; Step 'dump' 'ProcSense.EditorTools.PatientFaceSetup.Dump' 'dump' '\[PatientFace\]'; Step 'gown' 'ProcSense.EditorTools.GownRigBuilder.Build' 'gown' '\[GownRig\]'; Step 'curtains' 'ProcSense.EditorTools.CurtainRigBuilder.Build' 'curt' '\[CurtainRig\]'; Step 'proxy' 'ProcSense.EditorTools.PatientShadowProxy.Build' 'proxy' '\[ShadowProxy\]' }
if ($Rigs) { Step 'contact' 'ProcSense.EditorTools.PatientContactShadows.Build' 'cont' '\[ContactShadows\]' }
Step 'materials' 'ProcSense.EditorTools.FinalLookMaterials.Apply' 'mat' '\[FinalLookMaterials\]'
if (-not $NoBake) { $x = @(); if ($Final) { $x = @('-final') }; if ($Rigs) { Step 'proxy on' 'ProcSense.EditorTools.PatientShadowProxy.EnableForBake' 'pon' '\[ShadowProxy\]' }; Step 'bake' 'ProcSense.EditorTools.GoldenHourLighting.Bake' 'bake' '\[GoldenHour\]' $x; if ($Rigs) { Step 'proxy off' 'ProcSense.EditorTools.PatientShadowProxy.DisableAfterBake' 'poff' '\[ShadowProxy\]' } }
if (-not $NoRefl) { Step 'reflections' 'ProcSense.EditorTools.GoldenHourLighting.Reflections' 'refl' '\[GoldenHour\]' }
"== captures"; $env:LIGHT_CAPTURE_DIR="$r\unity\Logs\Lighting\cap_$Tag"
$p=Start-Process $exe -ArgumentList @('-batchmode','-projectPath',"`"$r\unity`"",'-runTests','-testPlatform','PlayMode','-testFilter','LightingCaptureTools','-testResults',"`"$r\unity\Logs\fa_cap_$Tag.xml`"",'-logFile',"`"$r\unity\Logs\fa_cap_$Tag.log`"") -Wait -PassThru
$xml=[xml](Get-Content "$r\unity\Logs\fa_cap_$Tag.xml"); $tr=$xml.'test-run'; "capture total=$($tr.total) passed=$($tr.passed) failed=$($tr.failed)"
if ($Tests) {
  "== cloth tests"
  $t=Start-Process $exe -ArgumentList @('-batchmode','-projectPath',"`"$r\unity`"",'-runTests','-testPlatform','PlayMode','-testFilter','ClothMotionTests','-testResults',"`"$r\unity\Logs\fa_cloth_$Tag.xml`"",'-logFile',"`"$r\unity\Logs\fa_cloth_$Tag.log`"") -Wait -PassThru
  $cx=[xml](Get-Content "$r\unity\Logs\fa_cloth_$Tag.xml"); $ct=$cx.'test-run'; "cloth total=$($ct.total) passed=$($ct.passed) failed=$($ct.failed)"
}
