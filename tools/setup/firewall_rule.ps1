<# P3-NET-05: Windows firewall rule for the networked Quest demonstration. Inbound TCP 8000 (server) and UDP 47800 (discovery beacon), Private profile only.
   Run once from an elevated PowerShell:  pwsh tools/setup/firewall_rule.ps1 [-Remove]
   It refuses to open anything while the active network is Public. #>
param([switch]$Remove, [int]$Port = 8000)
$name = 'ProcSense demo (LAN)'
if ($Remove) { Remove-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue; Write-Output "removed '$name'"; exit 0 }
$profiles = Get-NetConnectionProfile | Where-Object { $_.IPv4Connectivity -ne 'Disconnected' }
if ($profiles | Where-Object { $_.NetworkCategory -eq 'Public' }) {
  Write-Error "A connected network is Public. Set the Wi-Fi to Private (Settings > Network > Properties) first; no rule was added."; exit 1
}
Remove-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue
New-NetFirewallRule -DisplayName $name -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port -Profile Private | Out-Null
New-NetFirewallRule -DisplayName "$name discovery" -Direction Inbound -Action Allow -Protocol UDP -LocalPort 47800 -Profile Private | Out-Null
Write-Output "added inbound TCP $Port and UDP 47800 on the Private profile"
