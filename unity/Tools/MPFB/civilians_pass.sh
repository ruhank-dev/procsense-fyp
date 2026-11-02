#!/usr/bin/env bash
# Build, pose and bake the civilian people (waiting room / corridor). Run from the repository root:  bash unity/Tools/MPFB/civilians_pass.sh [keys...]
set -e
ROOT="$(pwd)"; export BLENDER_USER_RESOURCES="$ROOT/build/mpfb/blender-user"
BL="$ROOT/build/blender/blender-4.5.14-windows-x64/blender.exe"
KEYS=("$@"); if [ ${#KEYS[@]} -eq 0 ]; then KEYS=(patient_m_old patient_f_old adult_f_phone child parent_f visitor_m_paper visitor_f_talk visitor_m_cough); fi
"$BL" -b --factory-startup --python-exit-code 1 -P unity/Tools/MPFB/build_civilians.py -- "${KEYS[@]}" 2>&1 | grep -E "CIVILIAN_BUILT|Error|Traceback" | head -20
for k in "${KEYS[@]}"; do
  "$BL" -b --factory-startup --python-exit-code 1 -P unity/Tools/MPFB/staff_pose.py -- "$k" --bake 2>&1 | grep -E "Error|Traceback|EXPORT_COMPLETE" | cut -c1-160 | head -4
  "$BL" -b "build/mpfb/output/staff/${k}_baked.blend" -P unity/Tools/MPFB/preview_civilian.py -- "$ROOT/build/previews/${k}.png" 2>&1 | grep -E "Error|Traceback" | head -2 || true
done
echo "civilians pass done"
