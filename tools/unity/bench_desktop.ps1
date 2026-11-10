param([string]$Configs = 'B0,B1', [string]$Tag = 'run', [int]$Quality = 2, [int]$Frames = 20, [switch]$Png, [string]$Hide = '', [switch]$Stereo, [string]$Filter = 'DesktopRenderBenchmark', [int]$Rounds = 5, [double]$EyeScale = 1.0, [string]$Workloads = 'static,turn,walk', [ValidateSet('', 'cpu', 'gpu')][string]$Skin = '')
# Desktop render benchmark of the Quest views (P3-PRF-19): unity/Logs/bench/<Tag>.csv. Desktop numbers only; never Quest frame rates. One Unity at a time. Runs hidden (no window, no focus change).
#   -Stereo : render both eyes (64 mm apart) per frame, as the headset does.
#   -Filter StereoBenchmark : the two-eye interleaved benchmark (-Rounds, -EyeScale, -Workloads); compare with bench_report.py stereo.
#   -Skin   : 'cpu' or 'gpu' sets Player Settings > GPU Skinning for this run only and puts the project file back afterwards.
# large temporary files go to the drive with the most free space (D:\ProcSenseWork when present; override with PROCSENSE_WORK)
$work = if ($env:PROCSENSE_WORK) { $env:PROCSENSE_WORK } elseif (Test-Path 'D:\') { 'D:\ProcSenseWork' } else { $null }
if ($work) { New-Item -ItemType Directory -Force "$work\tmp" | Out-Null; $env:TEMP = "$work\tmp"; $env:TMP = "$work\tmp"; $env:GRADLE_USER_HOME = "$work\gradle" }
$r = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$exe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
$out = "$r\unity\Logs\bench"; New-Item -ItemType Directory -Force $out | Out-Null
$env:BENCH_CONFIGS = $Configs; $env:BENCH_OUT = "$out\$Tag.csv"; $env:BENCH_QUALITY = "$Quality"; $env:BENCH_FRAMES = "$Frames"; $env:BENCH_HIDE = $Hide; $env:BENCH_PNG_DIR = $(if ($Png) { $(if ($work) { "$work\bench_png\$Tag" } else { "$out\${Tag}_png" }) } else { "" }); $env:BENCH_ROUNDS = "$Rounds"; $env:BENCH_EYE_SCALE = "$EyeScale"; $env:BENCH_WORKLOADS = $Workloads; $env:BENCH_STEREO = $(if ($Stereo) { '1' } else { '0' })
$ps = "$r\unity\ProjectSettings\ProjectSettings.asset"; $orig = $null
if ($Skin) { $orig = [IO.File]::ReadAllText($ps); $want = $(if ($Skin -eq 'gpu') { 'gpuSkinning: 1' } else { 'gpuSkinning: 0' }); [IO.File]::WriteAllText($ps, ($orig -replace 'gpuSkinning: \d', $want)) }
try {
    $p = Start-Process $exe -ArgumentList @('-batchmode', '-projectPath', "`"$r\unity`"", '-runTests', '-testPlatform', 'PlayMode', '-testFilter', $Filter, '-testResults', "`"$out\$Tag.xml`"", '-logFile', "`"$out\$Tag.log`"") -Wait -PassThru -WindowStyle Hidden
} finally { if ($orig -ne $null) { [IO.File]::WriteAllText($ps, $orig) } }
$x = [xml](Get-Content "$out\$Tag.xml"); $t = $x.'test-run'; "bench total=$($t.total) passed=$($t.passed) failed=$($t.failed) -> $out\$Tag.csv"
