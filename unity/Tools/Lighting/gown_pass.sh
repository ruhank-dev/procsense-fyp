#!/bin/bash
# Re-install the Blender-draped gown and rebuild its secondary-motion rig, one Unity at a time. Usage: gown_pass.sh <capture-tag> (capture skipped when no tag)
TAG="$1"
SD="$(cd "$(dirname "$0")" && pwd)"
cd "$SD/../.."
S="$SD/../Rooms/unity_run.ps1"
for step in "ProcSense.EditorTools.GownRigBuilder.Remove gr_remove" "ProcSense.EditorTools.PatientBayLayout.ImportRedrapedGown gr_import" "ProcSense.EditorTools.GownRigBuilder.Build gr_build"; do
  set -- $step; powershell -NoProfile -File "$S" -Method $1 -Log $2 2>&1 | grep -E "^exit|error CS" | head -2
  grep -hE "\[GownRig\]|\[PatientBayLayout\]|Exception" Logs/$2.log | grep -v "^  at\|probe bake" | head -2
done
[ -n "$TAG" ] && powershell -NoProfile -File Tools/Lighting/capture.ps1 -Tag "$TAG" 2>&1 | tail -2
