param([string]$Configs = 'B0,B1', [string]$Tag = 'run', [int]$Quality = 2, [int]$Frames = 20, [switch]$Png, [string]$Hide = '')
# Desktop render benchmark of the eight Quest views (P3-PRF-19): unity/Logs/bench/<Tag>.csv. Desktop numbers only; never Quest frame rates. One Unity at a time.
$r = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$exe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
$out = "$r\unity\Logs\bench"; New-Item -ItemType Directory -Force $out | Out-Null
$env:BENCH_CONFIGS = $Configs; $env:BENCH_OUT = "$out\$Tag.csv"; $env:BENCH_QUALITY = "$Quality"; $env:BENCH_FRAMES = "$Frames"; $env:BENCH_HIDE = $Hide; $env:BENCH_PNG_DIR = $(if ($Png) { "$out\${Tag}_png" } else { "" })
$p = Start-Process $exe -ArgumentList @('-batchmode', '-projectPath', "`"$r\unity`"", '-runTests', '-testPlatform', 'PlayMode', '-testFilter', 'DesktopRenderBenchmark', '-testResults', "`"$out\$Tag.xml`"", '-logFile', "`"$out\$Tag.log`"") -Wait -PassThru -WindowStyle Hidden
$x = [xml](Get-Content "$out\$Tag.xml"); $t = $x.'test-run'; "bench total=$($t.total) passed=$($t.passed) failed=$($t.failed) -> $out\$Tag.csv"
