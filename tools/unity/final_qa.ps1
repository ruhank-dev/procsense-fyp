<#
 One command for the final visual QA of the Quest optimisation (P3-17). Hidden processes only, one Unity at a time (D:\ProcSenseWork\unity.lock), big files on D:.
 Stages (all by default, or pick with -Stages):
   tests  : targeted PlayMode tests ExteriorVisibilityTests, CharacterPerfTests, DesktopRenderBenchmark smoke (2 configs, 3 frames)
   walk   : 1080p walkthrough frames, original (B20) and optimised (B29 or -Optimised)         -> <Work>\frames\final_walk
   diff   : same path at 640x360 with BENCH_DIFF=1 (missing geometry vs unculled / original exterior) -> <Work>\frames\final_diff
   sweep  : 4752-pose Sweep_poses (doors closed and open)                                      -> <Work>\frames\final_sweep
   qa     : walk_qa.py on both walkthroughs (blank/white/black, jump, pop, geometry, still-camera pop, flicker) -> <Work>\reports\qa
   video  : full optimised + full original (1080p), side-by-side comparison, comparison with difference -> <Work>\videos
 Usage:  pwsh -File tools/unity/final_qa.ps1 [-Optimised B29] [-Original B20] [-Stages tests,walk,diff,sweep,qa,video] [-Work D:\ProcSenseWork]
 Offline re-check of existing frames (no Unity at all):  pwsh -File tools/unity/final_qa.ps1 -Stages qa,video
 Exit code 1 when a test fails or walk_qa finds blank/white/black frames, missing geometry, still-camera pop or flicker.
#>
param(
  [string]$Optimised = 'B29',
  [string]$Original = 'B20',
  [string[]]$Stages = @('tests', 'walk', 'diff', 'sweep', 'qa', 'video'),
  [string]$Work = 'D:\ProcSenseWork',
  [string]$Ranges = '0-12,22-27,56-64,95-110,140-160',
  [string]$Tag = '_r3',
  [switch]$SkipUnityLock,
  [int]$LockWaitMinutes = 240
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$unity = Join-Path $repo 'unity'
$unityExe = 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe'
$lock = Join-Path $Work 'unity.lock'
$fr = Join-Path $Work 'frames'; $rep = Join-Path $Work 'reports\qa'; $vid = Join-Path $Work 'videos'
foreach ($d in $fr, $rep, $vid, (Join-Path $Work 'tmp'), (Join-Path $Work 'gradle')) { New-Item -ItemType Directory -Force $d | Out-Null }
$env:TEMP = Join-Path $Work 'tmp'; $env:TMP = $env:TEMP; $env:GRADLE_USER_HOME = Join-Path $Work 'gradle'
$script:fail = @()
function Say($m) { Write-Host ("[final_qa " + (Get-Date).ToUniversalTime().ToString('HH:mm:ss') + " UTC] " + $m) }

function Take-Lock {
  if ($SkipUnityLock) { return }
  $until = (Get-Date).AddMinutes($LockWaitMinutes)
  while (Test-Path $lock) {
    if ((Get-Date) -gt $until) { throw "unity.lock still held after $LockWaitMinutes min: $(Get-Content $lock -Raw)" }
    Start-Sleep -Seconds 30
  }
  "final_qa $PID $((Get-Date).ToUniversalTime().ToString('o'))" | Set-Content $lock
}
function Drop-Lock { if (-not $SkipUnityLock) { Remove-Item $lock -ErrorAction SilentlyContinue } }

# run one PlayMode filter, hidden window
function Run-Test([string]$filter, [string]$tag) {
  $o = Join-Path $unity 'Logs\rt'; New-Item -ItemType Directory -Force $o | Out-Null
  Remove-Item (Join-Path $o "$tag.xml") -ErrorAction SilentlyContinue
  $p = Start-Process $unityExe -ArgumentList @('-batchmode', '-projectPath', "`"$unity`"", '-runTests', '-testPlatform', 'PlayMode', '-testFilter', $filter,
        '-testResults', "`"$o\$tag.xml`"", '-logFile', "`"$o\$tag.log`"") -PassThru -WindowStyle Hidden
  $p.WaitForExit()
  try {
    $t = ([xml](Get-Content "$o\$tag.xml")).'test-run'
    Say "$tag total=$($t.total) passed=$($t.passed) failed=$($t.failed)"
    if ([int]$t.failed -gt 0 -or [int]$t.total -eq 0) { $script:fail += "test:${tag}" }
  } catch { Say "${tag}: no result XML (exit $($p.ExitCode))"; $script:fail += "test:${tag}" }
}

function Set-Walk([string]$dir, [string]$cfgs, [int]$w, [int]$h, [string]$diff, [string]$jpg) {
  $env:BENCH_CLIP_DIR = $dir; $env:BENCH_CONFIGS = $cfgs; $env:BENCH_W = "$w"; $env:BENCH_H = "$h"
  $env:BENCH_FRAMES_MAX = '99999'; $env:BENCH_DIFF = $diff; $env:BENCH_JPG = $jpg; $env:BENCH_SEG = ''
}

$unityStages = @('tests', 'walk', 'diff', 'sweep') | Where-Object { $Stages -contains $_ }
if ($unityStages) {
  Take-Lock
  try {
    if ($Stages -contains 'tests') {
      Say 'targeted tests'
      Run-Test 'ProcSense.Tests.PlayMode.ExteriorVisibilityTests' 'fq_exterior'
      Run-Test 'ProcSense.Tests.PlayMode.CharacterPerfTests' 'fq_character'
      $env:BENCH_CONFIGS = "$Original,$Optimised"; $env:BENCH_FRAMES = '3'; $env:BENCH_OUT = Join-Path $rep 'bench_smoke.csv'
      Run-Test 'DesktopRenderBenchmark.Benchmark_views' 'fq_bench_smoke'
    }
    if ($Stages -contains 'walk') {
      Say 'walkthrough 1080p'
      Set-Walk (Join-Path $fr "final_walk$Tag") "$Original,$Optimised" 1920 1080 '0' '1'
      Run-Test 'QuestWalkthrough.Walkthrough' 'fq_walk'
    }
    if ($Stages -contains 'diff') {
      Say 'walkthrough with per-frame difference against unculled and original exterior'
      Set-Walk (Join-Path $fr "final_diff$Tag") $Optimised 640 360 '1' '0'
      Run-Test 'QuestWalkthrough.Walkthrough' 'fq_diff'
    }
    if ($Stages -contains 'sweep') {
      Say 'pose sweep'
      Set-Walk (Join-Path $fr "final_sweep$Tag") $Optimised 640 360 '0' '0'
      Run-Test 'QuestWalkthrough.Sweep_poses' 'fq_sweep'
    }
  } finally { Drop-Lock }
}

if ($Stages -contains 'qa') {
  Say 'frame QA'
  foreach ($id in $Original, $Optimised) {
    $d = Join-Path $fr "final_walk$Tag\$id"; $c = Join-Path $fr "final_walk$Tag\${id}_walk.csv"
    if (-not (Test-Path $d)) { Say "missing frames $d"; $script:fail += "qa:missing:${id}"; continue }
    $csv = $c
    $dc = Join-Path $fr "final_diff$Tag\${id}_walk.csv"
    if ($id -eq $Optimised -and (Test-Path $dc)) {
      # the geometry columns come from the 640x360 BENCH_DIFF run; merge them by frame into the 1080p CSV so one report shows everything
      $merged = Join-Path $rep "${id}_merged.csv"
      $g = @{}; Import-Csv $dc | ForEach-Object { $g[$_.frame] = $_ }
      Import-Csv $c | ForEach-Object {
        $r = $g[$_.frame]
        if ($r) { $_.diff_cull_pct = $r.diff_cull_pct; $_.diff_orig_pct = $r.diff_orig_pct; $_.diff_cull_mean = $r.diff_cull_mean; $_.diff_orig_mean = $r.diff_orig_mean }
        $_
      } | Export-Csv $merged -NoTypeInformation
      $csv = $merged
    }
    & python (Join-Path $repo 'tools\unity\walk_qa.py') $d --csv $csv --out $rep --name $id --fail-on 'blank,white,black,flat,geom,static,flicker'
    if ($LASTEXITCODE -ne 0) { $script:fail += "qa:${id}" }
  }
}

if ($Stages -contains 'video') {
  Say 'videos'
  $A = Join-Path $fr "final_walk$Tag\$Original"; $B = Join-Path $fr "final_walk$Tag\$Optimised"; $csvA = Join-Path $fr "final_walk$Tag\${Original}_walk.csv"
  $wv = Join-Path $repo 'tools\unity\walk_video.ps1'
  & pwsh -NoProfile -File $wv -Mode full -DirA $B -CsvA (Join-Path $fr "final_walk$Tag\${Optimised}_walk.csv") -LabelA "$Optimised optimised" -Out (Join-Path $vid "walkthrough_optimised_full$Tag.mp4")
  & pwsh -NoProfile -File $wv -Mode full -DirA $A -CsvA $csvA -LabelA "$Original original" -Out (Join-Path $vid "walkthrough_original_full$Tag.mp4")
  & pwsh -NoProfile -File $wv -Mode compare -DirA $A -DirB $B -CsvA $csvA -LabelA 'Original' -LabelB 'Optimised' -Ranges $Ranges -Out (Join-Path $vid "comparison_original_vs_optimised$Tag.mp4")
  & pwsh -NoProfile -File $wv -Mode triple -DirA $A -DirB $B -CsvA $csvA -LabelA 'Original' -LabelB 'Optimised' -Ranges $Ranges -Out (Join-Path $vid "comparison_with_difference$Tag.mp4")
}

if ($script:fail) { Say ("FAILED: " + ($script:fail -join ', ')); exit 1 }
Say "all requested stages passed; outputs under $Work (frames, reports\qa, videos)"
