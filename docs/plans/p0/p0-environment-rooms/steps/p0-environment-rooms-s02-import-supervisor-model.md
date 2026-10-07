# p0-environment-rooms-s02 — Import the supervisor's hospital model (EXP-ENV-1, EXP-ENV-3)

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

Prerequisites (you): Supervisor's room model files.

## What
Import the base hospital model into the URP project, fix materials (Render Pipeline Converter), scale and axes, split into the three category-A spaces (OT, patient bay, corridor), map every shell to an A location or 'unused', and profile each room on the Quest.

## Why
Real rooms replace greybox; budgets must hold before anything is placed.

## How to implement
1. Keep an untouched copy of the original package (outside the repo or LFS) — we only work on imported copies.
2. Run Render Pipeline Converter (pink materials); verify 1 unit = 1 m with the 1.7 m reference capsule.
3. Remove unused wings/rooms from the room scenes (not from the original).
4. Room mapping table in the phase README: shell → A location / unused (EXP-ENV-3).
5. OVR Metrics per room (EXP-ENV-1): T-EN1 ≥ 72 fps, T-EN4 memory ≤ 2.5 GB.

## Verification
All three rooms load on the Quest within budget; mapping table complete.

## Files changed
`unity/Assets/_Project/Art/Rooms/*`, room scenes, README.

## Dependencies
p0-vr-platform-s04.

## Common issues
Pink materials → converter; wrong scale → fix import scale, not by scaling objects.

## Sources
Implementation Guide B-S8 Steps 1–2, 6–7, T-EN1, T-EN4.
