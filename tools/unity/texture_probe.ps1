<#
 Clinical-text compression probe (audit lane 3): does ASTC 6x6 (today's Android setting) or 4x4 change how posters, the notice board, the reception and TV screens, prints and the monitor UI read at a Quest eye?
 Hidden Unity processes, one at a time, big files on D:. Three launches of two steps each:  state U (RGBA32 reference, also derives and writes the camera poses), A66, A44.
   Step 1  Editor method TextureQualityProbe.Apply (Standalone-only import override; logs the format Unity really loaded)
   Step 2  PlayMode test TextureCropProbe.Capture_text_crops (renders every text surface at 1832x1920, 4x MSAA, from the stored pose)
 and a last CLEAR launch that restores the 19 .meta files byte for byte from unity/Logs/texprobe_meta_backup (in a finally block: it runs even when a step failed).
 Afterwards texture_probe_report.py (CPU only, BelowNormal priority) compares the crops offline. The report is invalid, and says so, when the poses hold no text, when an import state did not change the pixels, or
 when the loaded formats do not match the state.
 Cache: if the inputs (the 19 textures and their .meta files, both probe sources, the scenes, the quality settings and the Quest URP asset) are unchanged and a complete crop set exists, no Unity is launched.
 Hand-off to the benchmark lane: set -SkipUnityLock when this runs inside a launch sequence that already holds D:\ProcSenseWork\unity.lock.
 Usage: pwsh -File tools/unity/texture_probe.ps1 [-Work D:\ProcSenseWork] [-NoCache] [-SkipUnityLock] [-ReportOnly]
 Test filter (for a caller that builds its own launch): ProcSense.Tests.PlayMode.TextureCropProbe; environment TEXPROBE_DIR, TEXPROBE_TAG (U|A66|A44), TEXPROBE_SELECT=1 (state U only), TEXPROBE_FMT for the Editor method.
 Exit code: 0 ok, 1 a step failed, 2 git status of unity/Assets/Textures is not empty afterwards, 3 report invalid.
#>
param(
  [string]$Work = 'D:\ProcSenseWork',
  [switch]$NoCache,
  [switch]$SkipUnityLock,
  [switch]$ReportOnly,
  [int]$LockWaitMinutes = 240
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$unity = Join-Path $repo 'unity'
$unityExe = 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe'
$lock = Join-Path $Work 'unity.lock'
$out = Join-Path $Work 'reports\lane3'; $crops = Join-Path $out 'crops'
foreach ($d in $out, $crops, (Join-Path $Work 'tmp'), (Join-Path $Work 'gradle')) { New-Item -ItemType Directory -Force $d | Out-Null }
$env:TEMP = Join-Path $Work 'tmp'; $env:TMP = $env:TEMP; $env:GRADLE_USER_HOME = Join-Path $Work 'gradle'; $env:PROCSENSE_WORK = $Work
$names = 'reception_screen', 'tv_welcome', 'bulletin_board', 'poster_01', 'poster_02', 'poster_03', 'poster_04', 'poster_05', 'poster_06', 'poster_10', 'poster_11', 'poster_12',
         'monitor_ui', 'monitor_labels'
function Say($m) { Write-Host ("[texture_probe " + (Get-Date).ToUniversalTime().ToString('HH:mm:ss') + " UTC] " + $m) }

# ---- input hash (cache key)
function Input-Hash {
  $files = @()
  foreach ($n in $names) { $files += Get-ChildItem (Join-Path $unity 'Assets\Textures') -Recurse -File | Where-Object { $_.Name -eq "$n.png" -or $_.Name -eq "$n.png.meta" } }
  $files += Get-Item (Join-Path $unity 'Assets\Tests\PlayMode\TextureCropProbe.cs'), (Join-Path $unity 'Assets\Editor\Setup\TextureQualityProbe.cs'), (Join-Path $unity 'ProjectSettings\QualitySettings.asset'), (Join-Path $unity 'Assets\Settings\URP_Quest_Android.asset')
  $files += Get-ChildItem (Join-Path $unity 'Assets\Scenes') -Filter *.unity
  $sb = New-Object System.Text.StringBuilder
  foreach ($f in ($files | Sort-Object FullName)) { [void]$sb.Append($f.FullName.Substring($unity.Length)).Append(':').Append((Get-FileHash $f.FullName -Algorithm SHA256).Hash).Append(';') }
  $bytes = [Text.Encoding]::UTF8.GetBytes($sb.ToString())
  ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($bytes))).Replace('-', '')
}

function Take-Lock {
  if ($SkipUnityLock) { return }
  $until = (Get-Date).AddMinutes($LockWaitMinutes)
  while (Test-Path $lock) {
    if ((Get-Date) -gt $until) { throw "unity.lock still held after $LockWaitMinutes min: $(Get-Content $lock -Raw)" }
    Start-Sleep -Seconds 30
  }
  "texture_probe $PID $((Get-Date).ToUniversalTime().ToString('o'))" | Set-Content $lock
}
function Drop-Lock { if (-not $SkipUnityLock) { Remove-Item $lock -ErrorAction SilentlyContinue } }

$script:failed = $false
function Run-Editor([string]$fmt) {
  $env:TEXPROBE_FMT = $fmt
  $log = Join-Path $out "apply_$fmt.log"
  $p = Start-Process $unityExe -ArgumentList @('-batchmode', '-nographics', '-projectPath', "`"$unity`"", '-executeMethod', 'ProcSense.EditorTools.TextureQualityProbe.Apply', '-logFile', "`"$log`"") -PassThru -WindowStyle Hidden
  $p.WaitForExit(); Say "apply $fmt exit $($p.ExitCode)"
  if ($p.ExitCode -ne 0) { $script:failed = $true; Say "apply $fmt FAILED (exit code 3 = .meta differs after restore, 4 = import state not applied)" }
}
function Run-Probe([string]$state, [bool]$select) {
  $env:TEXPROBE_DIR = $crops; $env:TEXPROBE_TAG = $state; $env:TEXPROBE_SELECT = if ($select) { '1' } else { '0' }
  $o = Join-Path $unity 'Logs\rt'; New-Item -ItemType Directory -Force $o | Out-Null
  Remove-Item (Join-Path $o "texprobe_$state.xml") -ErrorAction SilentlyContinue
  $p = Start-Process $unityExe -ArgumentList @('-batchmode', '-projectPath', "`"$unity`"", '-runTests', '-testPlatform', 'PlayMode', '-testFilter', 'ProcSense.Tests.PlayMode.TextureCropProbe',
        '-testResults', "`"$o\texprobe_$state.xml`"", '-logFile', "`"$o\texprobe_$state.log`"") -PassThru -WindowStyle Hidden
  $p.WaitForExit()
  try { $t = ([xml](Get-Content "$o\texprobe_$state.xml")).'test-run'; Say "capture $state total=$($t.total) passed=$($t.passed) failed=$($t.failed)"; if ([int]$t.failed -gt 0 -or [int]$t.total -eq 0) { $script:failed = $true } }
  catch { Say "capture ${state}: no result XML"; $script:failed = $true }
}

$hash = Input-Hash; $stamp = Join-Path $crops 'inputs.sha256'; $done = Join-Path $crops 'complete.flag'
$cached = (-not $NoCache) -and (Test-Path $done) -and (Test-Path $stamp) -and ((Get-Content $stamp -Raw).Trim() -eq $hash)
if ($ReportOnly) { $cached = $true }
if ($cached) { Say "inputs unchanged (sha256 $($hash.Substring(0, 12))): crops reused, no Unity launched" }
else {
  Remove-Item $done -ErrorAction SilentlyContinue; Remove-Item (Join-Path $crops 'poses.json') -ErrorAction SilentlyContinue
  Take-Lock
  try {
    foreach ($s in 'U', 'T', 'A66', 'A44') {
      Run-Editor $(if ($s -eq 'T') { 'CLEAR' } else { $s })   # T = today's shipped import (no override)
      if ($script:failed) { break }
      Run-Probe $s ($s -eq 'U')
      if ($script:failed) { break }
    }
  } finally {
    Run-Editor 'CLEAR'   # always: restores the .meta files, even after a failure
    Drop-Lock
  }
  if (-not $script:failed) { $hash | Set-Content $stamp; 'ok' | Set-Content $done }
}

# ---- offline report: CPU only, BelowNormal priority
$py = Join-Path $PSScriptRoot 'texture_probe_report.py'
$rp = Start-Process python -ArgumentList @('-I', "`"$py`"", '--dir', "`"$out`"", '--doc', "`"$(Join-Path $repo 'docs/qa/audit/lane3_textures_memory.md')`"") -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $out 'report.out') -RedirectStandardError (Join-Path $out 'report.err')
try { $rp.PriorityClass = [Diagnostics.ProcessPriorityClass]::BelowNormal } catch { }
$rp.WaitForExit(); Say "report exit $($rp.ExitCode): $(Join-Path $out 'texture_probe_report.md')"

$dirty = (& git -C $repo status --short -- unity/Assets/Textures)
$code = 0
if ($script:failed) { $code = 1 }
elseif ($dirty) { Say "WARNING unity/Assets/Textures is not clean:`n$dirty"; $code = 2 }
elseif ($rp.ExitCode -ne 0) { $code = 3 }
exit $code
