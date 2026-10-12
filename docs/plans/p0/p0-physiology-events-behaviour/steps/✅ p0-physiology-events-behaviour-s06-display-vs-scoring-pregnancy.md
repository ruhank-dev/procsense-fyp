# p0-physiology-events-behaviour-s06 — Display vs scoring; pregnancy physiology

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
The monitor shows display values (sensor-gated, with artefacts like a poor SpO2 probe); display zones follow walkthrough Part 17 while NEWS2 uses its own bands (temperature and HR differ by design — WT-I04). Pregnancy adjustments (raised HR and WBC per walkthrough) and NEWS2 disabled for pregnant patients (maternity tool placeholder).

## Why
Walkthrough separates display ranges from scoring bands; maternity cases need different physiology and tools.

## How to implement
1. Observable exposes display values + display zone; NEWS2 engine reads raw values with scoring bands.
2. Sensor artefact flags (probe poorly placed → unreliable SpO2/pleth) from interaction state.
3. Pregnancy modifiers in `content/physio/pregnancy.json` (values from walkthrough, register rows); used by VAR-S2-H (P1) — mechanism built now.

## Verification
`dotnet test --filter FullyQualifiedName~DisplayVsScoring` green: same state → walkthrough display zone vs NEWS2 points; T-SEP2 (pregnancy → NEWS2 not used).

## Files changed
`src/ProcSense.Core/Physiology/*`, `content/physio/pregnancy.json`, tests.

## Dependencies
p0-physiology-events-behaviour-s01; p0-clinical-logic-s03.

## Common issues
Don't merge display and scoring tables to 'simplify' — they differ on purpose.

## Sources
B-R13 Steps 10–11; Change Report WT-I04.
