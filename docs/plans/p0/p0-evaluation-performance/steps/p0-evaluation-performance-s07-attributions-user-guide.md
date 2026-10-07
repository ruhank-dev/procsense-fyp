# p0-evaluation-performance-s07 — Attributions page and user guide

Milestone: **M9 P0 release**

## What
In-app and docs attributions generated from the registers (guidelines cited, datasets, images, voices, sounds, character set, tools) and a short trainee/teacher guide (controls, scenarios, debrief).

## Why
Every external source used — guidelines, datasets, images, voices, sounds, characters, packages — must be credited correctly, and trainees/teachers need a short guide to use the simulator without the team present.

## How to implement
1. `tools/eval/attributions.py` → `docs/ATTRIBUTIONS.md` + in-app page.
2. Sources for attributions are only the registers: `docs/research/source_register.csv` (guidelines cited), `dataset_register.csv`, `content/images/index.json`, `tools/voice/voices.yaml`, `docs/research/sounds.csv`, the character-set ADR, third-party packages (Unity packages, NuGet, uv lockfiles).
3. `tools/eval/attributions.py` groups by kind, de-duplicates, and fails if any register row lacks an author/source field.
4. In-app: a 'Credits & sources' panel in the main menu reading a generated `attributions.json` from StreamingAssets.
5. User guide sections: controls (teleport, grab, tablet, push-to-talk in P1), how a case flows (Steps 1–14), what end states mean (postponement = success), the debrief, practice vs assessment mode, safety statement (educational only).

## Verification
`task attributions` regenerates `docs/ATTRIBUTIONS.md` with no missing fields; a teammate who has never used ProcSense completes VAR-S1-A Step 1 using only `docs/USER_GUIDE.md`.

## Files changed
`docs/ATTRIBUTIONS.md`, `docs/USER_GUIDE.md`, in-app page.

## Dependencies
M8.

## Common issues
Attribution text required by some sources has exact wording — copy it from the source page into the register.

## Sources
Master Plan §46 (references); docs/research registers; inventory D78–D82 (asset/voice/sound licences recorded).
