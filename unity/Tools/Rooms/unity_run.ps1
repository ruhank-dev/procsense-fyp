param([string]$Method, [string]$Log, [string[]]$Extra = @())
# Runs one Unity editor method in batch mode (log: unity/Logs/<Log>.log). Set UNITY_EXE to use a Unity install outside the Hub default folder.
$r = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$exe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
$logPath = "$r\unity\Logs\$Log.log"
$argList = @('-batchmode', '-quit', '-projectPath', "`"$r\unity`"", '-logFile', "`"$logPath`"", '-executeMethod', $Method) + $Extra
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process $exe -ArgumentList $argList -Wait -PassThru
"exit $($p.ExitCode) after $([int]$sw.Elapsed.TotalSeconds)s"
Select-String $logPath -Pattern 'error CS|Exception|Aborting|BedReview|FAILED|\[BedUpgrade\]' | Select-Object -First 25 | ForEach-Object { $_.Line.Trim() }
