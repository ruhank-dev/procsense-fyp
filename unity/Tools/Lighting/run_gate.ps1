param([string]$Tag = 'g1', [switch]$SkipBuild)
# Regression gate: full EditMode, full PlayMode, then the Quest APK build. One Unity at a time.
$s='C:\Users\RUHAN\AppData\Local\Temp\claude\c--Users-RUHAN-Downloads-procsense-fyp-main\5f6e9808-39a5-4600-a178-679c9fcf001e\scratchpad\unity_run.ps1'
$r='C:\Users\RUHAN\Downloads\procsense-fyp-main\procsense-fyp-main'; $exe='C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe'
foreach ($mode in 'EditMode','PlayMode') {
  "== $mode"
  $a=@('-batchmode','-projectPath',"`"$r\unity`"",'-runTests','-testPlatform',$mode,'-testResults',"`"$r\unity\Logs\gate_${mode}_$Tag.xml`"",'-logFile',"`"$r\unity\Logs\gate_${mode}_$Tag.log`"")
  $p=Start-Process -FilePath $exe -ArgumentList $a -Wait -PassThru
  $x=[xml](Get-Content "$r\unity\Logs\gate_${mode}_$Tag.xml"); $t=$x.'test-run'
  "$mode total=$($t.total) passed=$($t.passed) failed=$($t.failed) skipped=$($t.skipped) inconclusive=$($t.inconclusive)"
}
if (-not $SkipBuild) { "== quest build"; & $s -Method ProcSense.EditorTools.BuildScript.BuildQuest -Log "gate_build_$Tag" | Select-String 'error CS|Build succeeded|Build failed|BuildResult|size' | Select-Object -First 6 }
"== gate done"
