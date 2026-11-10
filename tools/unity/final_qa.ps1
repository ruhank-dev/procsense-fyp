<#
 One command for the final visual QA of the Quest optimisation (P3-17). Hidden processes only, one Unity at a time (D:\ProcSenseWork\unity.lock), big files on D:.
 Stages (all by default, or pick with -Stages):
   tests  : targeted PlayMode tests ExteriorVisibilityTests, CharacterPerfTests, DesktopRenderBenchmark smoke (2 configs, 3 frames)
   walk   : walkthrough frames, original (B20) and optimised (B29 or -Optimised)                -> <Work>\frames\final_walk<Tag>
   diff   : same path at 640x360 with BENCH_DIFF=1 (missing geometry vs unculled / original exterior) -> <Work>\frames\final_diff<Tag>
   sweep  : 4752-pose Sweep_poses (doors closed and open; camera-path-only changes do not need it)  -> <Work>\frames\final_sweep<Tag>
   qa     : walk_qa.py on both walkthroughs, one process per config, parallel decoding (blank/white/black, jump, pop, geometry, still-camera pop, flicker) -> <Work>\reports\qa
   video  : full optimised + full original, side-by-side comparison, comparison with difference; NVENC when available, all four encodes in parallel -> <Work>\videos
 Modes:
   -Mode final  (default) 1920x1080 frames, all detectors, Tag _r3 unless given.
   -Mode quick  640x360 frames and videos, BENCH_DIFF in the same run (no separate diff stage), Tag _quick: a fast preliminary look while iterating; run -Mode final once at the end.
 Frame cache (frame_cache.py, <Work>\frame_cache): a walk or diff run whose inputs (scenes, shaders, scripts, materials, prefabs, URP and project settings, textures and models by size and time,
   the walkthrough test) and options are unchanged is not rendered again; the cached frames are linked in. A changed input re-renders. -Segments doors,windows (or segment names such as doorfast,baywindow) renders only
   those path segments and overlays them onto the newest cached full set of the same config and resolution (use it when only part of the path or one room changed; the rest is the earlier render).
   Aliases: doors=doorfast, windows=baywindow,glasswall, turns=glass360, bay, corridor, theatre, glasswall, return.
 Overlap: video encoding of the finished walk frames runs in the background while the diff and sweep stages use Unity.
 Usage:  pwsh -File tools/unity/final_qa.ps1 [-Mode final|quick] [-Optimised B29] [-Original B20] [-Stages ...] [-Segments doors] [-Tag _r5] [-Work D:\ProcSenseWork] [-Exact]
 Offline re-check of existing frames (no Unity at all):  pwsh -File tools/unity/final_qa.ps1 -Stages qa,video -Tag _r4
 Exit code 1 when a test fails or walk_qa finds blank/white/black frames, missing geometry, still-camera pop or flicker.
#>
param(
  [ValidateSet('final', 'quick')][string]$Mode = 'final',
  [string]$Optimised = 'B29',
  [string]$Original = 'B20',
  [string[]]$Stages = @('tests', 'walk', 'diff', 'sweep', 'qa', 'video'),
  [string]$Work = 'D:\ProcSenseWork',
  [string]$Ranges = '0-12,22-27,56-64,95-110,140-160',
  [string]$Tag = '',
  [string]$Segments = '',
  [switch]$Exact,
  [switch]$NoCache,
  [switch]$SkipUnityLock,
  [int]$LockWaitMinutes = 240
)
$ErrorActionPreference = 'Stop'
$Stages = @($Stages | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })   # pwsh -File passes 'walk,qa' as one string
if (-not $Tag) { $Tag = if ($Mode -eq 'quick') { '_quick' } else { '_r3' } }
$quick = ($Mode -eq 'quick')
$W = if ($quick) { 640 } else { 1920 }; $H = if ($quick) { 360 } else { 1080 }
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$unity = Join-Path $repo 'unity'
$unityExe = 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe'
$lock = Join-Path $Work 'unity.lock'
$fr = Join-Path $Work 'frames'; $rep = Join-Path $Work 'reports\qa'; $vid = Join-Path $Work 'videos'
foreach ($d in $fr, $rep, $vid, (Join-Path $Work 'tmp'), (Join-Path $Work 'gradle')) { New-Item -ItemType Directory -Force $d | Out-Null }
$env:TEMP = Join-Path $Work 'tmp'; $env:TMP = $env:TEMP; $env:GRADLE_USER_HOME = Join-Path $Work 'gradle'; $env:PROCSENSE_WORK = $Work
$script:fail = @()
$fc = Join-Path $PSScriptRoot 'frame_cache.py'
$dirWalk = Join-Path $fr "final_walk$Tag"; $dirDiff = Join-Path $fr "final_diff$Tag"; $dirSweep = Join-Path $fr "final_sweep$Tag"
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
    if ([int]$t.failed -gt 0 -or [int]$t.total -eq 0) { $script:fail += "test:$tag" }
  } catch { Say "${tag}: no result XML (exit $($p.ExitCode))"; $script:fail += "test:$tag" }
}

function Set-Walk([string]$dir, [string]$cfgs, [int]$w, [int]$h, [string]$diff, [string]$jpg, [string]$seg = '') {
  $env:BENCH_CLIP_DIR = $dir; $env:BENCH_CONFIGS = $cfgs; $env:BENCH_W = "$w"; $env:BENCH_H = "$h"
  $env:BENCH_FRAMES_MAX = '99999'; $env:BENCH_DIFF = $diff; $env:BENCH_JPG = $jpg; $env:BENCH_SEG = $seg
}

function Seg-Names {
  $map = @{ doors = 'doorfast'; windows = 'baywindow,glasswall'; turns = 'glass360' }
  (($Segments -split ',' | ForEach-Object { $k = $_.Trim(); if ($map.ContainsKey($k)) { $map[$k] } else { $k } }) -join ',')
}

# frame set for one config: cache hit -> link, else render (whole path, or only -Segments merged onto the newest full set), then store and link
function Ensure-Frames([string]$cfg, [string]$dest, [int]$w, [int]$h, [string]$diff, [string]$jpg, [string]$test, [string]$tag) {
  $o = @('--cfg', $cfg, '--w', "$w", '--h', "$h", '--diff', $diff, '--jpg', $jpg)
  if (-not $NoCache -and -not $Segments) {
    & python $fc get @o *> $null
    if ($LASTEXITCODE -eq 0) { & python $fc link @o --dest $dest | Out-Null; Say "$tag ${cfg}: cache hit, not rendered"; return }
  }
  $stage = Join-Path $Work "tmp\stage_$tag$cfg"; Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
  $seg = ''
  if ($Segments) { $seg = Seg-Names; Say "$tag ${cfg}: rendering only segments $seg" }
  Set-Walk $stage $cfg $w $h $diff $jpg $seg
  Take-Lock
  try { Run-Test $test $tag } finally { Drop-Lock }
  if ($Segments) {
    & python $fc merge @o --src $stage --dest (Join-Path $Work "tmp\merged_$tag$cfg") | Out-Null
    if ($LASTEXITCODE -ne 0) { Say "$tag ${cfg}: no cached full set to merge into, rendering the whole path"; Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue; Set-Walk $stage $cfg $w $h $diff $jpg ''; Take-Lock; try { Run-Test $test $tag } finally { Drop-Lock } }
    else { $stage = Join-Path $Work "tmp\merged_$tag$cfg" }
  }
  if (-not (Test-Path (Join-Path $stage "${cfg}_walk.csv"))) { Say "$tag ${cfg}: no CSV produced"; $script:fail += "walk:$cfg"; return }
  & python $fc put @o --src $stage | Out-Null
  & python $fc link @o --dest $dest | Out-Null
}

$unityStages = @('tests', 'walk', 'diff', 'sweep') | Where-Object { $Stages -contains $_ }
if ($Stages -contains 'tests') {
  Say 'targeted tests'
  Take-Lock
  try {
    Run-Test 'ProcSense.Tests.PlayMode.ExteriorVisibilityTests' 'fq_exterior'
    Run-Test 'ProcSense.Tests.PlayMode.CharacterPerfTests' 'fq_character'
    $env:BENCH_CONFIGS = "$Original,$Optimised"; $env:BENCH_FRAMES = '3'; $env:BENCH_OUT = Join-Path $rep 'bench_smoke.csv'
    Run-Test 'DesktopRenderBenchmark.Benchmark_views' 'fq_bench_smoke'
  } finally { Drop-Lock }
}
$videoJob = $null
if ($Stages -contains 'walk') {
  Say "walkthrough $($W)x$($H) ($Mode)"
  $d = if ($quick) { '1' } else { '0' }   # quick: the difference columns come from the same run
  foreach ($c in $Original, $Optimised) {
    $dd = if ($quick -and $c -eq $Optimised) { '1' } else { '0' }
    Ensure-Frames $c $dirWalk $W $H $dd '1' 'QuestWalkthrough.Walkthrough' "fq_walk_$c"
  }
  # encode the videos from the finished frames while the Unity stages below keep running
  if (($Stages -contains 'video') -and ($Stages -contains 'diff' -or $Stages -contains 'sweep')) {
    Say 'videos start in the background'
    $videoJob = Start-Job -ScriptBlock { param($f, $a) & pwsh -NoProfile -File $f @a } -ArgumentList $PSCommandPath, @('-Mode', $Mode, '-Stages', 'video', '-Tag', $Tag, '-Work', $Work, '-Ranges', $Ranges)
  }
}
if (($Stages -contains 'diff') -and -not $quick) {
  Say 'walkthrough with per-frame difference against unculled and original exterior'
  Ensure-Frames $Optimised $dirDiff 640 360 '1' '0' 'QuestWalkthrough.Walkthrough' 'fq_diff'
}
if ($Stages -contains 'sweep') {
  Say 'pose sweep'
  Set-Walk $dirSweep $Optimised 640 360 '0' '0'
  Take-Lock
  try { Run-Test 'QuestWalkthrough.Sweep_poses' 'fq_sweep' } finally { Drop-Lock }
}

if ($Stages -contains 'qa') {
  Say 'frame QA (one process per config, parallel decoding, per-frame feature cache)'
  $jobs = @()
  foreach ($id in $Original, $Optimised) {
    $d = Join-Path $dirWalk $id; $c = Join-Path $dirWalk "${id}_walk.csv"
    if (-not (Test-Path $d)) { Say "missing frames $d"; $script:fail += "qa:missing:${id}"; continue }
    $csv = $c
    $dc = if ($quick) { $c } else { Join-Path $dirDiff "${id}_walk.csv" }
    if ($id -eq $Optimised -and -not $quick -and (Test-Path $dc)) {
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
    $extra = @(); if ($Exact) { $extra += '--exact' }
    $jobs += Start-Job -ScriptBlock { param($py, $s, $d, $csv, $rep, $id, $extra) $ErrorActionPreference = 'Continue'; & python $s $d --csv $csv --out $rep --name $id --workers 5 --fail-on 'blank,white,black,flat,geom,static,flicker' @extra 2>&1 | ForEach-Object { "$_" }; "EXIT=$LASTEXITCODE" } `
      -ArgumentList 'python', (Join-Path $PSScriptRoot 'walk_qa.py'), $d, $csv, $rep, $id, $extra
  }
  foreach ($j in $jobs) {
    $out = Receive-Job $j -Wait -ErrorAction Continue; Remove-Job $j
    $out | ForEach-Object { Write-Host $_ }
    if (($out | Select-Object -Last 1) -ne 'EXIT=0') { $script:fail += "qa:job" }
  }
}

if ($videoJob) { Say 'waiting for the background videos'; Receive-Job $videoJob -Wait | Out-Null; Remove-Job $videoJob }
elseif ($Stages -contains 'video') {
  Say 'videos (NVENC when available, four encodes in parallel)'
  $A = Join-Path $dirWalk $Original; $B = Join-Path $dirWalk $Optimised; $csvA = Join-Path $dirWalk "${Original}_walk.csv"; $csvB = Join-Path $dirWalk "${Optimised}_walk.csv"
  $wv = Join-Path $PSScriptRoot 'walk_video.ps1'
  $pw = if ($quick) { 0 } else { 0 }
  $segArg = @(); if ($Segments) { $segArg = @('-Segments', (Seg-Names)) }
  $specs = @(
    @('-Mode', 'full', '-DirA', $B, '-CsvA', $csvB, '-LabelA', "$Optimised optimised", '-Out', (Join-Path $vid "walkthrough_optimised_full$Tag.mp4")),
    @('-Mode', 'full', '-DirA', $A, '-CsvA', $csvA, '-LabelA', "$Original original", '-Out', (Join-Path $vid "walkthrough_original_full$Tag.mp4")),
    @('-Mode', 'compare', '-DirA', $A, '-DirB', $B, '-CsvA', $csvA, '-LabelA', 'Original', '-LabelB', 'Optimised', '-Ranges', $Ranges, '-Out', (Join-Path $vid "comparison_original_vs_optimised$Tag.mp4")),
    @('-Mode', 'triple', '-DirA', $A, '-DirB', $B, '-CsvA', $csvA, '-LabelA', 'Original', '-LabelB', 'Optimised', '-Ranges', $Ranges, '-Out', (Join-Path $vid "comparison_with_difference$Tag.mp4"))
  )
  if ($Segments) { $specs = $specs[2..3] | ForEach-Object { , ($_ + $segArg) } }
  $vj = foreach ($s in $specs) { Start-Job -ScriptBlock { param($f, $a) $ErrorActionPreference = 'Continue'; & pwsh -NoProfile -File $f @a 2>&1 | ForEach-Object { "$_" }; "EXIT=$LASTEXITCODE" } -ArgumentList $wv, $s }
  foreach ($j in $vj) { $out = Receive-Job $j -Wait -ErrorAction Continue; Remove-Job $j; if (($out | Select-Object -Last 1) -ne 'EXIT=0') { $script:fail += 'video'; $out | Select-Object -Last 5 | ForEach-Object { Write-Host $_ } } }
}

if ($script:fail) { Say ("FAILED: " + ($script:fail -join ', ')); exit 1 }
Say "all requested stages passed; outputs under $Work (frames, reports\qa, videos)"
