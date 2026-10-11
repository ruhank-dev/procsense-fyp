# p0-physiology-events-behaviour-s02 — Couplings and realistic noise

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Data-driven couplings (volume deficit → HR ↑ SBP ↓; pain/anxiety → HR ↑ BP ↑ RR ↑; hypoxia/hypoglycaemia → confusion flag for the behaviour model) and seeded AR(1) noise per variable (phi ≈ 0.9, small σ from VitalDB aggregates) that never exceeds the 'uncaused change' band.

## Why
Real monitors flicker and parameters move together; without this the patient looks scripted.

## How to implement
1. `content/physio/couplings.json` (rule rows: source var → target var, gain, Type D) — not hard-coded.
2. `Physiology/Noise.cs`: AR(1) per variable on SimRandom stream 'physio'; σ per variable from `content/physio/noise.json` (p0-medical-research-data-s15).
3. Uncaused-change band per variable in content; noise amplitude capped below it.
4. Tests: T-P2 — 1000 seeds, no actions → no change beyond the noise band; couplings move dependants in the right direction.

## Verification
`dotnet test --filter FullyQualifiedName~Couplings` + `dotnet test --filter FullyQualifiedName~Noise` → T-P2 green; determinism hash unchanged across runs.

## Files changed
`src/ProcSense.Core/Physiology/Couplings.cs`, `Noise.cs`, `content/physio/couplings.json`, tests.

## Dependencies
p0-physiology-events-behaviour-s01; p0-medical-research-data-s15.

## Common issues
Noise crossing a NEWS2 band edge looks like a real change — keep σ small near band edges.

## Sources
B-R13 Steps 3–4, T-P2; inventory D31.
