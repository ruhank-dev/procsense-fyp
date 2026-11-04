param([string]$Tag = 'g1', [switch]$SkipBuild)
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
# Regression gate: full EditMode, full PlayMode, then the Quest APK build. One Unity at a time.
$s=(Join-Path $PSScriptRoot 'unity_run.ps1')
$r=$Repo; $exe=$UnityExe
foreach ($mode in 'EditMode','PlayMode') {
  "== $mode"
  $a=@('-batchmode','-projectPath',"`"$r\unity`"",'-runTests','-testPlatform',$mode,'-testResults',"`"$r\unity\Logs\gate_${mode}_$Tag.xml`"",'-logFile',"`"$r\unity\Logs\gate_${mode}_$Tag.log`"")
  $p=Start-Process -FilePath $exe -ArgumentList $a -Wait -PassThru
  $x=[xml](Get-Content "$r\unity\Logs\gate_${mode}_$Tag.xml"); $t=$x.'test-run'
  "$mode total=$($t.total) passed=$($t.passed) failed=$($t.failed) skipped=$($t.skipped) inconclusive=$($t.inconclusive)"
}
if (-not $SkipBuild) { "== quest build"; & $s -Method ProcSense.EditorTools.BuildScript.BuildQuest -Log "gate_build_$Tag" | Select-String 'error CS|Build succeeded|Build failed|BuildResult|size' | Select-Object -First 6 }
"== gate done"
