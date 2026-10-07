# p0-characters-s01 — Choose the base human set (ADR)

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

## What
Compare the candidate human sources (Microsoft Rocketbox avatars; MPFB 2 / MakeHuman in Blender) on: Quest poly budget (≤ 25k tris per staff character with 2 LODs, B-S3), rig compatibility with Mixamo/Humanoid, outfits available, facial blend shapes for visemes and expressions, skin-tone range; team picks one; ADR written.

## Why
Realistic humans aren't practical to script from scratch; one consistent base keeps the cast coherent.

## How to implement
1. Import 2 samples from each candidate into Unity; check Humanoid rig, retarget a Mixamo walk and talk clip.
2. Profile 1 patient + 3 staff on screen on Quest 2.
3. Check each candidate's terms when downloading (you said licence filtering isn't needed — record terms anyway in the ADR for the attributions page).
4. ADR `docs/decisions/ADR-0002-base-humans.md` with the decision and alternatives.

## Verification
ADR approved by you; the chosen set imports and animates cleanly.

## Files changed
`docs/decisions/ADR-0002-base-humans.md`, test imports.

## Dependencies
p0-vr-platform-s01.

## Common issues
Foot sliding → root motion off + speed matching (B-S3 troubleshooting).

## Sources
free-model-candidates.md (characters); Implementation Guide B-S3 Steps 1–2.
