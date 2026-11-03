<#
.SYNOPSIS
  Restores and verifies the ProcSense Unity/Blender environment after a clone.   Run from anywhere:  powershell -ExecutionPolicy Bypass -File tools\setup\restore_environment.ps1

.DESCRIPTION
  1. Checks the tools (Git, Git LFS, Unity Editor 6000.5.10f1, optional Python/Blender/ffmpeg).
  2. Downloads the Git LFS objects (baked lightmaps, reflection probes, the generated patient mesh).
  3. Verifies EVERY tracked asset listed in tools\setup\asset_manifest.json by SHA-256 (text files are compared with LF line endings), so a missing file, a Git LFS pointer
     instead of the real data, a corrupt copy or a changed .meta (GUID) is reported by name. Missing or corrupt files are restored from Git (git checkout / git lfs pull).
     Files you changed yourself are reported as "modified" and never overwritten unless you pass -Force.
  4. Optional downloads from the original sources (every download is checked against the SHA-256, MD5 or Git blob SHA-1 recorded in the manifest):
       -DownloadSources           all sources that have a direct download (ambientCG, Poly Haven, Rocketbox, MakeHuman) into build\external\cache
       -Source <id>[,<id>]        only these manifest source ids (see docs/new_unity_environment.md)
       -WithBlender               Blender 4.5.14 portable into build\blender (needed only to rebuild models with unity\Tools\*)
  5. Manual downloads (no direct link): put the file in build\external\manual\ and rerun; the file is checked and installed where the manifest says.
  6. -ImportInUnity runs Unity in batch mode once so that packages are restored, every asset is imported and the C# compiles (about 20-40 minutes the first time).

  Safe to rerun. Exit code 0 = everything verified, 1 = something is still missing or different (the list is printed).
#>
[CmdletBinding()]
param(
  [switch]$Offline,
  [switch]$Force,
  [switch]$DownloadSources,
  [string[]]$Source,
  [switch]$WithBlender,
  [switch]$ImportInUnity,
  [switch]$VerifyOnly,
  [string]$UnityExe = $env:UNITY_EXE
)

$ErrorActionPreference = 'Stop'
if ($Source) { $Source = @($Source | ForEach-Object { $_ -split '[,; ]+' } | Where-Object { $_ }) }      # powershell -File passes "a,b" as one string
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$ManifestPath = Join-Path $PSScriptRoot 'asset_manifest.json'
$Cache = Join-Path $Repo 'build\external\cache'
$Manual = Join-Path $Repo 'build\external\manual'
$Problems = New-Object System.Collections.Generic.List[string]
function Say($m, $c = 'Gray') { Write-Host $m -ForegroundColor $c }
function Ok($m) { Write-Host "  [ok]   $m" -ForegroundColor Green }
function Warn($m) { Write-Host "  [warn] $m" -ForegroundColor Yellow }
function Bad($m) { Write-Host "  [FAIL] $m" -ForegroundColor Red; $script:Problems.Add($m) }

# --- 1. tools ---------------------------------------------------------------------------------------------------------------------------------
Say "== 1. Tools" Cyan
$manifest = Get-Content -Raw -Encoding UTF8 $ManifestPath | ConvertFrom-Json
$want = $manifest.unity_editor
foreach ($t in @('git')) { if (Get-Command $t -ErrorAction SilentlyContinue) { Ok ("$t " + ((& $t --version) -join ' ')) } else { Bad "$t is not installed (https://git-scm.com/download/win)" } }
$lfsVer = $null; try { $lfsVer = (& git lfs version) 2>$null } catch {}
if ($lfsVer) { Ok $lfsVer } else { Bad 'Git LFS is not installed (included in Git for Windows; or https://git-lfs.com). Run: git lfs install' }
if (-not $UnityExe) { $UnityExe = "C:\Program Files\Unity\Hub\Editor\$want\Editor\Unity.exe" }
if (Test-Path $UnityExe) {
  Ok "Unity Editor $want found: $UnityExe"
  $android = Join-Path (Split-Path $UnityExe) 'Data\PlaybackEngines\AndroidPlayer'
  if (Test-Path $android) { Ok 'Android Build Support module installed' } else { Warn 'Android Build Support module not installed (needed only for Quest/APK builds): Unity Hub > Installs > Add modules' }
} else { Warn "Unity Editor $want not found at $UnityExe. Install it through Unity Hub (unityhub://$want/$($manifest.unity_editor_revision)) or set UNITY_EXE. Verification of files still runs." }
foreach ($t in @('python', 'ffmpeg')) { if (Get-Command $t -ErrorAction SilentlyContinue) { Ok "$t found (optional)" } else { Warn "$t not found (optional: Python rebuilds models, ffmpeg encodes capture videos)" } }
$blenderExe = Join-Path $Repo 'build\blender\blender-4.5.14-windows-x64\blender.exe'
if (Test-Path $blenderExe) { Ok 'Blender 4.5.14 portable found in build\blender' } else { Warn 'Blender 4.5.14 not in build\blender (optional; run this script with -WithBlender)' }

# --- 2. Git LFS -------------------------------------------------------------------------------------------------------------------------------
Say "== 2. Git LFS objects" Cyan
Push-Location $Repo
try {
  $inGit = (& git rev-parse --is-inside-work-tree 2>$null) -eq 'true'
  if ($inGit -and $lfsVer -and -not $Offline) { & git lfs install --skip-repo 2>$null | Out-Null; & git lfs pull; if ($LASTEXITCODE -eq 0) { Ok 'git lfs pull finished' } else { Bad 'git lfs pull failed (network or LFS quota); rerun later' } }
  elseif ($Offline) { Warn 'offline: skipped git lfs pull' }
} finally { Pop-Location }

# --- 3. verify every tracked asset -------------------------------------------------------------------------------------------------------------
Say "== 3. Verifying $($manifest.files.Count) tracked files by SHA-256" Cyan
$textExt = @('.cs','.meta','.unity','.prefab','.mat','.asset','.shader','.cginc','.hlsl','.json','.md','.txt','.py','.ps1','.sh','.yaml','.yml','.asmdef','.asmref','.shadergraph','.shadersubgraph','.controller','.anim','.overrideController','.mask','.physicMaterial','.renderTexture','.flare','.cubemap','.playable','.signal','.inputactions','.uss','.uxml','.xml','.csv','.lighting','.preset','.mixer','.guiskin','.fontsettings','.spriteatlas','.terrainlayer','.brush','.compute','.raytrace','.vfx','.tpsd')
$sha = [System.Security.Cryptography.SHA256]::Create()
function Get-NormHash([string]$full, [string]$ext) {
  $bytes = [System.IO.File]::ReadAllBytes($full)
  if ($textExt -contains $ext.ToLowerInvariant()) {
    $s = [System.Text.Encoding]::GetEncoding(28591).GetString($bytes).Replace("`r`n", "`n")
    $bytes = [System.Text.Encoding]::GetEncoding(28591).GetBytes($s)
  }
  return ([System.BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
}
function Get-State($f) {
  $full = Join-Path $Repo ($f.path -replace '/', '\')
  if (-not (Test-Path -LiteralPath $full)) { return @{ state = 'missing'; full = $full } }
  $len = (Get-Item -LiteralPath $full).Length
  if ($f.lfs -and $len -lt 200) { $head = [System.Text.Encoding]::ASCII.GetString([System.IO.File]::ReadAllBytes($full)); if ($head.StartsWith('version https://git-lfs')) { return @{ state = 'lfs-pointer'; full = $full } } }
  $h = Get-NormHash $full ([System.IO.Path]::GetExtension($full))
  if ($h -eq $f.sha256) { return @{ state = 'ok'; full = $full } } else { return @{ state = 'different'; full = $full } }
}
$bad = New-Object System.Collections.Generic.List[object]; $okCount = 0; $i = 0; $total = $manifest.files.Count
foreach ($f in $manifest.files) {
  $i++; if ($i % 500 -eq 0) { Write-Progress -Activity 'Verifying assets' -Status "$i / $total" -PercentComplete (100 * $i / $total) }
  $st = Get-State $f
  if ($st.state -eq 'ok') { $okCount++ } else { $bad.Add([pscustomobject]@{ file = $f; state = $st.state; full = $st.full }) }
}
Write-Progress -Activity 'Verifying assets' -Completed
Ok "$okCount of $total files match the manifest"

if ($bad.Count -gt 0 -and -not $VerifyOnly) {
  Say "   $($bad.Count) files differ: restoring from Git where possible" Yellow
  Push-Location $Repo
  try {
    foreach ($b in $bad) {
      $rel = $b.file.path
      $modified = $false
      if ($inGit -and $b.state -eq 'different') { $modified = [bool](& git status --porcelain -- $rel) }
      if ($modified -and -not $Force) { Warn "modified locally, kept: $rel   (pass -Force to restore the repository version)"; continue }
      if ($inGit) {
        if ($b.file.lfs -and -not $Offline) { & git lfs pull --include="$rel" 2>$null | Out-Null }
        & git checkout -- $rel 2>$null
        if ($b.file.lfs -and -not $Offline) { & git lfs checkout $rel 2>$null | Out-Null }
      }
    }
  } finally { Pop-Location }
  $bad2 = New-Object System.Collections.Generic.List[object]
  foreach ($b in $bad) {
    $st = Get-State $b.file
    if ($st.state -eq 'ok') { continue }
    if ($st.state -eq 'different' -and -not $Force) {
      Push-Location $Repo; $mod = [bool](& git status --porcelain -- $b.file.path); Pop-Location
      if ($mod) { Warn "modified (not overwritten): $($b.file.path)"; continue }
    }
    $bad2.Add([pscustomobject]@{ file = $b.file; state = $st.state })
  }
  $bad = $bad2
}
$byState = $bad | Group-Object state
foreach ($g in $byState) { Bad "$($g.Count) files $($g.Name): e.g. $($g.Group[0].file.path)" }
$unrestored = $bad.ToArray()

# --- 4. downloads from the original sources -----------------------------------------------------------------------------------------------------
function Get-FileChecked($url, $dest, $sha256, $md5, $gitBlob) {
  if (Test-Path $dest) { if (Test-Hash $dest $sha256 $md5 $gitBlob) { return $true } }
  New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
  $ProgressPreference = 'SilentlyContinue'
  Invoke-WebRequest -Uri $url -OutFile $dest -UserAgent 'ProcSense-setup' -UseBasicParsing
  return (Test-Hash $dest $sha256 $md5 $gitBlob)
}
function Test-Hash($path, $sha256, $md5, $gitBlob) {
  if ($sha256) { return ((Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant() -eq $sha256.ToLowerInvariant()) }
  if ($md5) { return ((Get-FileHash -Algorithm MD5 -LiteralPath $path).Hash.ToLowerInvariant() -eq $md5.ToLowerInvariant()) }
  if ($gitBlob) {
    $b = [System.IO.File]::ReadAllBytes($path); $hdr = [System.Text.Encoding]::ASCII.GetBytes("blob $($b.Length)`0"); $sha1 = [System.Security.Cryptography.SHA1]::Create()
    $all = New-Object byte[] ($hdr.Length + $b.Length); [Array]::Copy($hdr, 0, $all, 0, $hdr.Length); [Array]::Copy($b, 0, $all, $hdr.Length, $b.Length)
    return (([System.BitConverter]::ToString($sha1.ComputeHash($all))).Replace('-', '').ToLowerInvariant() -eq $gitBlob.ToLowerInvariant())
  }
  return $true
}
function Install-Source($rec) {
  Say "-- $($rec.id): $($rec.name)" White
  if (-not $rec.downloads -or $rec.downloads.Count -eq 0) { Warn 'no direct download (see the manual steps in docs/new_unity_environment.md)'; return }
  $dir = Join-Path $Cache $rec.id; $fail = 0; $n = 0
  foreach ($d in $rec.downloads) {
    $n++; $name = ($d.path -replace '[\\/]', '__'); $dest = Join-Path $dir $name
    try { $good = Get-FileChecked $d.url $dest $d.sha256 $d.md5 $d.git_blob_sha1 } catch { $good = $false; Warn "download failed: $($d.url) ($($_.Exception.Message))" }
    if (-not $good) { $fail++; if (Test-Path $dest) { Remove-Item -LiteralPath $dest -Force }; Bad "$($rec.id): checksum or download failed for $($d.path)" }
  }
  if ($fail -eq 0) { Ok "$($rec.id): $n files downloaded and checksum-verified into build\external\cache\$($rec.id)" }
}
if (($DownloadSources -or $Source -or $WithBlender) -and -not $Offline) {
  Say "== 4. Downloads from the original sources" Cyan
  $ids = @()
  if ($WithBlender) { $ids += 'blender_tool' }
  if ($Source) { $ids += $Source }
  $recs = $manifest.sources | Where-Object { $_.downloads -and (($DownloadSources -and $_.id -ne 'blender_tool') -or ($ids -contains $_.id)) }
  foreach ($r in $recs) { Install-Source $r }
  if ($WithBlender) {
    $zip = Join-Path (Join-Path $Cache 'blender_tool') 'blender-4.5.14-windows-x64.zip'
    if ((Test-Path $zip) -and -not (Test-Path $blenderExe)) { Say '   extracting Blender ...'; Expand-Archive -LiteralPath $zip -DestinationPath (Join-Path $Repo 'build\blender') -Force; if (Test-Path $blenderExe) { Ok 'Blender 4.5.14 installed in build\blender' } else { Bad 'Blender extraction did not produce blender.exe' } }
  }
  if ($DownloadSources -and (Get-Command python -ErrorAction SilentlyContinue)) { Say '   Rocketbox avatars/textures can now be rebuilt with:  python -I unity\Tools\Characters\fetch_rocketbox.py' }
}
if (Test-Path $Manual) {
  $found = Get-ChildItem -LiteralPath $Manual -File -ErrorAction SilentlyContinue
  if ($found) { Say "== Manual downloads in build\external\manual: $($found.Count) file(s)" Cyan; foreach ($m in $found) { Ok "$($m.Name) $([math]::Round($m.Length/1MB,1)) MB (extract it to the destination listed in docs/new_unity_environment.md, then rerun this script)" } }
}

# --- 5. Unity import ---------------------------------------------------------------------------------------------------------------------------
if ($ImportInUnity) {
  Say "== 5. Unity batch import (restores packages, imports assets, compiles scripts)" Cyan
  if (-not (Test-Path $UnityExe)) { Bad "Unity Editor not found at $UnityExe" }
  else {
    $log = Join-Path $Repo 'unity\Logs\restore_import.log'; New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
    $p = Start-Process $UnityExe -ArgumentList @('-batchmode', '-quit', '-projectPath', "`"$(Join-Path $Repo 'unity')`"", '-logFile', "`"$log`"") -PassThru -Wait
    $errs = Select-String -Path $log -Pattern 'error CS\d+|Scripts have compiler errors|Missing Script|Failed to import|Could not load|Library\\\S+ is missing' -ErrorAction SilentlyContinue
    if ($p.ExitCode -eq 0 -and -not $errs) { Ok 'Unity imported the project: no compile errors, no import failures in the log' }
    else { Bad "Unity exit code $($p.ExitCode); $(@($errs).Count) error lines in $log"; $errs | Select-Object -First 8 | ForEach-Object { Warn $_.Line.Trim() } }
  }
}

# --- summary --------------------------------------------------------------------------------------------------------------------------------------
Say "== Summary" Cyan
if ($unrestored.Count -eq 0 -and $Problems.Count -eq 0) { Say 'All manifest files verified. Environment restored.' Green; exit 0 }
else { Say "$($Problems.Count) problem(s):" Red; $Problems | ForEach-Object { Say "  - $_" Red }; Say 'See docs/new_unity_environment.md, section "Troubleshooting".' Yellow; exit 1 }
