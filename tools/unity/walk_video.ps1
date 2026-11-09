<#
 Build walkthrough videos from Unity frame folders with ffmpeg only (hidden, libx264 crf 18, yuv420p).

 -Mode full     : one config, captioned (segment | room | time from CSV)      -> <Out>
 -Mode compare  : original | optimised side by side for -Ranges                -> <Out>
 -Mode triple   : original | optimised | amplified difference for -Ranges      -> <Out>
 -Mode all      : full (config A) + compare + triple, Out is a base path (suffixes _full/_compare/_triple)

 Examples:
  pwsh -File tools/unity/walk_video.ps1 -Mode full -DirA unity/Logs/walk_full/B20 -CsvA unity/Logs/walk_full/B20_walk.csv -LabelA "B20 original" -Out out/walk_B20.mp4
  pwsh -File tools/unity/walk_video.ps1 -Mode compare -DirA .../B20 -DirB .../B17 -CsvA .../B20_walk.csv -LabelA "Original" -LabelB "Optimised" -Ranges "0-10,31-40" -Out out/cmp.mp4
  pwsh -File tools/unity/walk_video.ps1 -Mode triple  ... -Amplify 8 -Out out/diff.mp4
 Ranges are seconds "start-end" separated by commas.
#>
param(
  [ValidateSet('full', 'compare', 'triple', 'all')][string]$Mode = 'full',
  [Parameter(Mandatory)][string]$DirA,
  [string]$DirB,
  [string]$CsvA,
  [string]$LabelA = 'A',
  [string]$LabelB = 'B',
  [int]$Fps = 30,
  [string]$Ranges = '0-10',
  [int]$PanelWidth = 0,      # per-panel width; 0 = 960 for compare, 640 for triple, native for full
  [double]$Amplify = 8,
  [int]$Crf = 18,
  [string]$Font = 'C\\:/Windows/Fonts/arial.ttf',   # double escape: filtergraph level + option level
  [Parameter(Mandatory)][string]$Out
)
$ErrorActionPreference = 'Stop'
$ffmpeg = (Get-Command ffmpeg -ErrorAction Stop).Source
$tmp = Join-Path ([IO.Path]::GetTempPath()) ('walkvid_' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $tmp | Out-Null
New-Item -ItemType Directory -Force (Split-Path -Parent ([IO.Path]::GetFullPath($Out))) | Out-Null

function Run-FFmpeg([string[]]$a) {
  Write-Host ("ffmpeg " + ($a -join ' '))
  $p = Start-Process -FilePath $ffmpeg -ArgumentList $a -WindowStyle Hidden -Wait -PassThru -RedirectStandardError (Join-Path $tmp 'ff.log')
  if ($p.ExitCode -ne 0) { Get-Content (Join-Path $tmp 'ff.log') -Tail 15 | Write-Host; throw "ffmpeg failed ($($p.ExitCode))" }
}
function Q([string]$s) { if ($s -match '\s') { '"' + $s + '"' } else { $s } }

# caption chain: segment/room runs from CSV (absolute frame numbers; S = first frame of this clip)
function Caption([string]$label, [string]$csv, [int]$S) {
  $fs = 'fontfile=' + $Font + ':fontcolor=white:box=1:boxcolor=black@0.55:boxborderw=6'
  $o = @()
  $safeLabel = $label -replace "[':\\%,]", ' '
  $o += "drawtext=$fs`:fontsize=h/30:x=12:y=10:text='$safeLabel'"
  $off = $S / $Fps
  $o += "drawtext=$fs`:fontsize=h/30:x=w-tw-12:y=10:text='t=%{eif\:trunc(t+$off)\:d}.%{eif\:mod(trunc((t+$off)*10),10)\:d} s'"
  if ($csv -and (Test-Path $csv)) {
    $rows = Import-Csv $csv
    $start = 0
    for ($i = 1; $i -le $rows.Count; $i++) {
      $changed = ($i -eq $rows.Count) -or ($rows[$i].seg -ne $rows[$start].seg) -or ($rows[$i].room -ne $rows[$start].room)
      if ($changed) {
        $txt = (($rows[$start].seg + ' | ' + $rows[$start].room) -replace "[':\\%,]", ' ')
        $lo = $start; $hi = $i - 1
        $o += "drawtext=$fs`:fontsize=h/32:x=12:y=h-th-12:enable='between(n+$S,$lo,$hi)':text='$txt'"
        $start = $i
      }
    }
  }
  return ($o -join ',')
}

function Panel([int]$idx, [string]$label, [string]$csv, [int]$S, [int]$w) {
  $sc = if ($w -gt 0) { "scale=${w}:-2," } else { '' }
  return "[$idx`:v]${sc}" + (Caption $label $csv $S) + "[p$idx]"
}

function Make-Clip([string]$mode, [int]$S, [int]$count, [string]$dest) {
  $in = @()
  $inA = @('-framerate', $Fps, '-start_number', $S, '-i', (Q (Join-Path $DirA 'f%05d.jpg')))
  $inB = if ($DirB) { @('-framerate', $Fps, '-start_number', $S, '-i', (Q (Join-Path $DirB 'f%05d.jpg'))) } else { @() }
  if ($mode -eq 'full') {
    $w = $PanelWidth
    $fc = (Panel 0 $LabelA $CsvA $S $w) + ';[p0]scale=out_range=tv,format=yuv420p[v]'
    $args2 = $inA
  } elseif ($mode -eq 'compare') {
    $w = if ($PanelWidth -gt 0) { $PanelWidth } else { 960 }
    $fc = (Panel 0 $LabelA $CsvA $S $w) + ';' + (Panel 1 $LabelB $CsvA $S $w) + ';[p0][p1]hstack=inputs=2,scale=out_range=tv,format=yuv420p[v]'
    $args2 = $inA + $inB
  } else {
    $w = if ($PanelWidth -gt 0) { $PanelWidth } else { 640 }
    $amp = $Amplify.ToString([Globalization.CultureInfo]::InvariantCulture)
    $lut = "clip(val*$amp,0,255)"
    $cap = Caption "difference x$amp" $CsvA $S
    $pa = (Caption $LabelA $CsvA $S); $pb = (Caption $LabelB $CsvA $S)
    $fc = "[0:v]split[a0][a1];[1:v]split[b0][b1];" +
          "[a0]scale=${w}:-2,$pa[p0];[b0]scale=${w}:-2,$pb[p1];" +
          "[a1]format=gbrp[a2];[b1]format=gbrp[b2];[a2][b2]blend=all_mode=difference,lutrgb=r='$lut':g='$lut':b='$lut',scale=${w}:-2,$cap[p2];" +
          "[p0][p1][p2]hstack=inputs=3,scale=out_range=tv,format=yuv420p[v]"
    $args2 = $inA + $inB
  }
  $fcFile = Join-Path $tmp ("fc_" + [guid]::NewGuid().ToString('N') + '.txt')
  [IO.File]::WriteAllText($fcFile, $fc, (New-Object Text.UTF8Encoding $false))
  $a = $args2 + @('-y', '-hide_banner', '-loglevel', 'error', '-/filter_complex', (Q $fcFile), '-map', '[v]',
        '-frames:v', $count, '-r', $Fps, '-c:v', 'libx264', '-crf', $Crf, '-preset', 'medium', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', (Q $dest))
  Run-FFmpeg $a
}

function Make-Ranged([string]$mode, [string]$dest) {
  $parts = @()
  $k = 0
  foreach ($r in ($Ranges -split ',')) {
    $se = $r.Trim() -split '-'
    $s0 = [int][math]::Round([double]$se[0] * $Fps)
    $cnt = [int][math]::Round(([double]$se[1] - [double]$se[0]) * $Fps)
    $p = Join-Path $tmp ("r{0}.mp4" -f $k++)
    Make-Clip $mode $s0 $cnt $p
    $parts += $p
  }
  if ($parts.Count -eq 1) { Copy-Item $parts[0] $dest -Force; return }
  $list = Join-Path $tmp 'list.txt'
  ($parts | ForEach-Object { "file '" + ($_ -replace '\\', '/') + "'" }) | Set-Content $list -Encoding ascii
  Run-FFmpeg @('-y', '-hide_banner', '-loglevel', 'error', '-f', 'concat', '-safe', '0', '-i', (Q $list), '-c', 'copy', (Q $dest))
}

try {
  $total = (Get-ChildItem $DirA -Filter 'f*.jpg').Count
  if ($Mode -in 'compare', 'triple', 'all' -and -not $DirB) { throw 'DirB required for compare/triple' }
  switch ($Mode) {
    'full'    { Make-Clip 'full' 0 $total $Out }
    'compare' { Make-Ranged 'compare' $Out }
    'triple'  { Make-Ranged 'triple' $Out }
    'all' {
      $b = [IO.Path]::ChangeExtension($Out, $null).TrimEnd('.')
      Make-Clip 'full' 0 $total ($b + '_full.mp4')
      Make-Ranged 'compare' ($b + '_compare.mp4')
      Make-Ranged 'triple' ($b + '_triple.mp4')
    }
  }
  Write-Host "done: $Out"
} finally {
  Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}
