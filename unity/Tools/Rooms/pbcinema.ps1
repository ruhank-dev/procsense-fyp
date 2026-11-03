param([string]$Tag = 'cine', [int]$Frames = 96)
# Cinematic go-through of the patient bay: five camera moves (door entry, bed and patient, sun streaks on the floor, window shafts and dust, wide finish) recorded at a fixed 1/12 s step,
# joined and smoothed to 24 fps with ffmpeg. Output: unity\Logs\Rooms\PatientBay_<Tag>\PatientBay_<Tag>_gothrough.mp4
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$moves = @(
  @{ n = 'a_entry';   v = '{"views":[{"n":"start","p":[2.25,1.62,-0.8],"l":[-1.8,1.7,0.3],"f":62},{"n":"end","p":[0.7,1.55,-0.55],"l":[-1.9,1.55,0.4],"f":62}]}' },
  @{ n = 'b_patient'; v = '{"views":[{"n":"start","p":[1.5,1.55,-1.5],"l":[0.0,0.85,1.1],"f":58},{"n":"end","p":[0.65,1.35,-0.35],"l":[0.0,0.8,1.15],"f":50}]}' },
  @{ n = 'c_floor';   v = '{"views":[{"n":"start","p":[1.9,0.55,1.7],"l":[-0.6,0.05,-0.6],"f":72},{"n":"end","p":[1.1,0.5,1.3],"l":[-1.6,0.05,-0.2],"f":72}]}' },
  @{ n = 'd_window';  v = '{"views":[{"n":"start","p":[-0.5,1.45,-1.35],"l":[-2.2,1.9,0.3],"f":52},{"n":"end","p":[-1.1,1.4,-0.8],"l":[-2.2,1.9,0.55],"f":52}]}' },
  @{ n = 'e_wide';    v = '{"views":[{"n":"start","p":[2.25,1.55,-1.75],"l":[-1.8,1.5,0.4],"f":72},{"n":"end","p":[1.55,1.6,-1.45],"l":[-1.8,1.45,0.5],"f":66}]}' }
)
$list = @()
foreach ($m in $moves) {
  $json = Join-Path $Repo "unity\Tools\Rooms\cine_$($m.n).json"; Set-Content -LiteralPath $json -Value $m.v -Encoding UTF8
  & (Join-Path $PSScriptRoot 'room_video.ps1') -Scene PatientBay -Views "Tools\Rooms\cine_$($m.n).json" -Tag "${Tag}_$($m.n)" -Frames $Frames | Select-Object -Last 2
  $list += (Join-Path $Repo "unity\Logs\Rooms\PatientBay_${Tag}_$($m.n)\PatientBay_${Tag}_$($m.n).mp4")
}
$ff = if ($env:FFMPEG) { $env:FFMPEG } else { (Get-Command ffmpeg).Source }
$out = Join-Path $Repo "unity\Logs\Rooms\PatientBay_${Tag}"; New-Item -ItemType Directory -Force $out | Out-Null
$txt = Join-Path $out 'list.txt'; Set-Content -LiteralPath $txt -Value ($list | ForEach-Object { "file '" + ($_ -replace '\\', '/') + "'" }) -Encoding ASCII
& $ff -y -loglevel error -f concat -safe 0 -i $txt -vf "minterpolate=fps=24:mi_mode=mci:mc_mode=aobmc:vsbmc=1,fade=t=in:st=0:d=0.8" -c:v libx264 -pix_fmt yuv420p -crf 18 (Join-Path $out "PatientBay_${Tag}_gothrough.mp4")
"cinema done: $out"
