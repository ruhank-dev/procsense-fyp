# p0-physiology-events-behaviour-s05 — Treatment effects library

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Cause → effect curves for fluids, analgesia, oxygen, antiemetics, warming, positioning (head-up/flat), and adverse effects (e.g. EFF-ANAPHYLAXIS following the walkthrough 14.4 pattern, improving after clinician-given treatment); referenced by ID from treatments and events.

## Why
Feedback only teaches if the patient visibly responds to what the trainee started: fluids raise BP and slow HR, oxygen raises SpO2, analgesia lowers pain and HR — and an allergen given causes the walkthrough's reaction pattern.

## How to implement
1. `content/physio/effects.json`: effect rows {id, targets, profile, source, evidence_type D} with register rows.
2. Treatments (p0-clinical-logic-s10) and events (s08) emit effect IDs; engine applies them.
3. Tests T-P1 per effect: direction + timing within band; oxygen raises SpO2 in a hypoxia fixture.
4. Effect magnitudes reviewed as plots in s07 before any case depends on them.

## Verification
`dotnet test --filter FullyQualifiedName~Effects` green; every effect has a plot from s07.

## Files changed
`content/physio/effects.json`, register rows, tests.

## Dependencies
p0-physiology-events-behaviour-s01.

## Common issues
Effects are Type D — the medical reviewer approves them as plots, not numbers.

## Sources
B-R13 worked example, T-P1; B-M14 Step 3; walkthrough 14.4; inventory D37.
