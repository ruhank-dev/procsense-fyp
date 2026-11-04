param([Parameter(Mandatory)][string]$Tag, [string[]]$Sets = @('patientbay','corridor','reception','ot','exterior'))
# Captures the full QA view sets with real Unity URP renders: unity/Logs/Rooms/<Scene>_<Tag>_<set>/ (one Unity at a time). Corridor-world sets render in the Corridor scene, patientbay in PatientBay, ot in OT.
$u = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$scene = @{ patientbay = 'PatientBay'; corridor = 'Corridor'; reception = 'Corridor'; ot = 'OT'; exterior = 'Corridor' }
foreach ($s in $Sets) {
  "== $s"; & (Join-Path $u 'Tools\Rooms\room_capture.ps1') -Scene $scene[$s] -Views (Join-Path $PSScriptRoot "views\$s.json") -Tag "${Tag}_$s" -Width 1600
}
"qa capture done"
