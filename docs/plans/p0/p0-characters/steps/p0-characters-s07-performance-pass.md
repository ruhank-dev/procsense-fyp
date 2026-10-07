# p0-characters-s07 — Characters performance pass (EXP-CHR-2)

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

## What
Profile patient + up to 5 staff in the OT on Quest 2; tune LODs, texture sizes, skinned-mesh counts and blend-shape counts.

## Why
Skinned characters with blend shapes are typically the biggest CPU/GPU cost on Quest; the OT during Time Out has the most people on screen at once, so it's the worst case to design for.

## How to implement
1. OVR Metrics capture in the OT with the full cast; Unity profiler skinning cost (T-AV2, T-ST-A2).
2. Adjust and record EXP-CHR-2.
3. Worst case: patient + anaesthetist + surgeon + scrub + circulating + porter in the OT during Time Out.
4. Levers: LOD bias, skin weights ≤ 2 bones for distant LODs, texture size 1K, blend shapes only on LOD0.

## Verification
OVR Metrics capture within budget recorded in the characters README (EXP-CHR-2).

## Files changed
`docs/plans/p0/p0-characters/README.md` (EXP-CHR-2 result), LOD settings on character prefabs in `unity/Assets/_Project/Art/Characters/*`, profiler captures in `docs/plans/p0/p0-vr-platform/perf/`.

## Dependencies
p0-characters-s05.

## Common issues
Profiling in the Editor is meaningless for Quest — always capture on device. Disabling blend shapes on distant LODs is invisible to users but saves a lot.

## Sources
Implementation Guide B-S2 T-AV2, B-S3 config (LOD ×2, ≤ 25k tris) and T-ST-A2; B-S11 perf gate.
