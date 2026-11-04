param([string[]]$Groups)
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' }
# Runs each filter in its own Unity session and prints pass/fail counts, to find which test leaks state into the later ones.
$r=(Join-Path $Repo 'unity'); $exe=$UnityExe
$i = 0
foreach ($g in $Groups) {
  $i++; $x="$r\Logs\bisect_$i.xml"; if (Test-Path $x) { Remove-Item $x -Force }
  $a=@('-batchmode','-projectPath',"`"$r`"",'-runTests','-testPlatform','PlayMode','-testFilter',"`"$g`"",'-testResults',"`"$x`"",'-logFile',"`"$r\Logs\bisect_$i.log`"")
  $p=Start-Process -FilePath $exe -ArgumentList $a -Wait -PassThru
  $t=([xml](Get-Content $x)).'test-run'
  "$g -> total=$($t.total) passed=$($t.passed) failed=$($t.failed)"
}
"bisect done"
