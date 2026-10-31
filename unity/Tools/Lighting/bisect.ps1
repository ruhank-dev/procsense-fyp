param([string[]]$Groups)
# Runs each filter in its own Unity session and prints pass/fail counts, to find which test leaks state into the later ones.
$r='C:\Users\RUHAN\Downloads\procsense-fyp-main\procsense-fyp-main\unity'; $exe='C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe'
$i = 0
foreach ($g in $Groups) {
  $i++; $x="$r\Logs\bisect_$i.xml"; if (Test-Path $x) { Remove-Item $x -Force }
  $a=@('-batchmode','-projectPath',"`"$r`"",'-runTests','-testPlatform','PlayMode','-testFilter',"`"$g`"",'-testResults',"`"$x`"",'-logFile',"`"$r\Logs\bisect_$i.log`"")
  $p=Start-Process -FilePath $exe -ArgumentList $a -Wait -PassThru
  $t=([xml](Get-Content $x)).'test-run'
  "$g -> total=$($t.total) passed=$($t.passed) failed=$($t.failed)"
}
"bisect done"
