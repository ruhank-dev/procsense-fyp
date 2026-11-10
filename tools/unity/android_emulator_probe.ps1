# Reproduces the Android emulator feasibility checks. No elevation, no GUI, nothing launched unless acceleration is OK.
# Usage: powershell -NoProfile -File tools\unity\android_emulator_probe.ps1 [-Install] [-Boot]
param([switch]$Install, [switch]$Boot)
$ErrorActionPreference = 'Continue'
$A   = 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Data\PlaybackEngines\AndroidPlayer'
$Sdk = 'C:\Users\RUHAN\AndroidEmuSdk'
$env:JAVA_HOME = "$A\OpenJDK"; $env:ANDROID_SDK_ROOT = $Sdk
$adb = "$Sdk\platform-tools\adb.exe"
$sm = "$A\SDK\cmdline-tools\16.0\bin\sdkmanager.bat"
$am = "$Sdk\cmdline-tools\latest\bin\avdmanager.bat"   # copy of Unity cmdline-tools so avdmanager sees the new root
$emu = "$Sdk\emulator\emulator.exe"
$img = 'system-images;android-34;google_apis;x86_64'
if ($Install) {
  New-Item -ItemType Directory -Force $Sdk | Out-Null
  (1..30 | ForEach-Object { 'y' }) | & $sm --sdk_root=$Sdk --licenses | Out-Null   # accepts standard Android SDK licences
  & $sm --sdk_root=$Sdk 'emulator' $img
  New-Item -ItemType Directory -Force "$Sdk\cmdline-tools\latest" | Out-Null
  Copy-Item "$A\SDK\cmdline-tools\16.0\*" "$Sdk\cmdline-tools\latest" -Recurse -Force
  Copy-Item "$A\SDK\platform-tools" $Sdk -Recurse -Force   # emulator requires platform-tools in the root
  'no' | & $am create avd -n probe34 -k $img -d pixel_5 --force
}
Write-Host '--- hypervisor state (no elevation) ---'
(Get-CimInstance Win32_ComputerSystem).HypervisorPresent
Get-CimInstance Win32_DeviceGuard -Namespace root\Microsoft\Windows\DeviceGuard -ErrorAction SilentlyContinue | Select-Object VirtualizationBasedSecurityStatus
Write-Host '--- emulator -accel-check ---'
& $emu -accel-check 2>&1
$ok = ($LASTEXITCODE -eq 0)
if (-not $ok) { Write-Host 'Acceleration unavailable: not booting (software x86 emulation is not useful).'; exit 2 }
if ($Boot) {
  Start-Process $emu -ArgumentList '-avd probe34 -no-window -no-audio -no-snapshot -no-boot-anim -gpu swiftshader_indirect -memory 2048 -cores 2' -WindowStyle Hidden
  $t = Get-Date
  do { Start-Sleep 5; $b = (& $adb -s emulator-5554 shell getprop sys.boot_completed 2>$null) } until (($b -match '1') -or ((Get-Date)-$t).TotalSeconds -gt 300)
  "boot_seconds=" + [int]((Get-Date)-$t).TotalSeconds
  & $adb devices
  & $adb -s emulator-5554 shell "getprop ro.product.cpu.abilist; getprop ro.build.version.release; grep MemTotal /proc/meminfo; nproc"
  Start-Sleep 30
  $p = Get-Process qemu-system*; $c = $p.CPU; Start-Sleep 10; $p.Refresh()
  "qemu WS_MB=" + [int]($p.WorkingSet64/1MB) + " cores_busy=" + [math]::Round(($p.CPU-$c)/10,2)
  & $adb -s emulator-5554 emu kill | Out-Null   # only ever targets the emulator, never a physical device
}
