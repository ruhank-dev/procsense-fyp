# p0-clinical-logic-s03 — NEWS2 engine

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
NEWS2 calculation from content tables: Scale 1 and Scale 2 (COPD/hypercapnic variant), single-parameter-3 flag, total, response tier and reassessment interval from `news2_response.csv`; 'incomplete' when any parameter is missing; disabled for pregnant patients (maternity tool is institutional).

## Why
NEWS2 drives escalation, escort choice, observation frequency and S2 urgency.

## How to implement
1. `Clinical/News2.cs`: `Score(obs, scale) → {total, single3, tier, incomplete}` reading `news2_scoring.csv` (bands per parameter) — no numbers in code.
2. Store each score with sim timestamp in `clinical.news2[]` (trend for EMR + debrief).
3. Pregnancy flag → NEWS2 not used; maternity early-warning placeholder (Type C) (T-SEP2).
4. Nurse NPC sets observation frequency from the tier (walkthrough 4.9).
5. Tests from every NEWS2 worked example in walkthrough Part 4 (EXP-CL-1); a mismatch → issues.md, not a code tweak.

## Verification
`dotnet test --filter FullyQualifiedName~News2` green; VAR-S1-A baseline → NEWS2 0 (walkthrough Scene 3); every Part 4 worked example matches.

## Files changed
`src/ProcSense.Core/Clinical/News2.cs`, tests.

## Dependencies
p0-medical-research-data-s03.

## Common issues
Display zones ≠ scoring bands (WT-I04: temperature) — use the scoring table only.

## Sources
B-R18 Step 1, Step 4 (pregnancy), T-SEP2; walkthrough 4.9; inventory D18, D161, D206.
