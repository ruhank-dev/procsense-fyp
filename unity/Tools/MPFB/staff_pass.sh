#!/usr/bin/env bash
# Rebuild, pose and bake the two nurses (the lab coats changed), headless. Run from the repository root:  bash unity/Tools/MPFB/staff_pass.sh [keys...]
set -e
ROOT="$(pwd)"
export BLENDER_USER_RESOURCES="$ROOT/build/mpfb/blender-user"
BL="$ROOT/build/blender/blender-4.5.14-windows-x64/blender.exe"
KEYS=("$@"); if [ ${#KEYS[@]} -eq 0 ]; then KEYS=(nurse_m nurse_f); fi
"$BL" -b --factory-startup --python-exit-code 1 -P unity/Tools/MPFB/build_staff.py -- "${KEYS[@]}" 2>&1 | grep -E "REFINE_COAT|CLEARANCE|Error|Traceback|STAFF" | head -20
for k in "${KEYS[@]}"; do
  "$BL" -b --factory-startup --python-exit-code 1 -P unity/Tools/MPFB/staff_pose.py -- "$k" --bake 2>&1 | grep -E "Error|Traceback|BAKE|baked|exported" | head -10
done
echo "staff pass done"
