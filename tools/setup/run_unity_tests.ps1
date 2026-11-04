param([string]$Platform = 'PlayMode', [string]$Filter = '', [string]$Tag = 'run')
# Runs a subset (or all) of the Unity tests in batch mode and prints a summary; results in unity\Logs\<Tag>_<Platform>.xml. A stale result file is deleted first so it can never be mistaken for this run.
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$exe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
$xmlPath = Join-Path $Repo "unity\Logs\${Tag}_$Platform.xml"; $logPath = Join-Path $Repo "unity\Logs\${Tag}_$Platform.log"
if (Test-Path $xmlPath) { Remove-Item -LiteralPath $xmlPath -Force }
$args2 = @('-batchmode', '-projectPath', "`"$(Join-Path $Repo 'unity')`"", '-runTests', '-testPlatform', $Platform, '-testResults', "`"$xmlPath`"", '-logFile', "`"$logPath`"")
if ($Filter) { $args2 += @('-testFilter', "`"$Filter`"") }
$p = Start-Process $exe -ArgumentList $args2 -Wait -PassThru -NoNewWindow
if (-not (Test-Path $xmlPath)) { "no result file (exit $($p.ExitCode)); see $logPath"; Select-String -Path $logPath -Pattern 'error CS' | Select-Object -First 5 | ForEach-Object { $_.Line }; exit 1 }
$x = [xml](Get-Content -Raw $xmlPath); $r = $x.'test-run'
"$Platform total=$($r.total) passed=$($r.passed) failed=$($r.failed) skipped=$($r.skipped) (exit $($p.ExitCode))"
foreach ($tc in $x.SelectNodes('//test-case[@result="Failed"]')) { '  FAIL ' + $tc.fullname }
