# p0-clinical-logic-s10 — Treatment library, required checks and effects

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Treatments as content (medicine classes, fluids, oxygen devices, procedures like IV access) with required checks — identity, allergy known, indication, restriction, timing, order, documentation — and effect IDs that physiology applies. No doses unless reviewer-approved display text.

## Why
Step 8 treatments must be safe, checked and visibly effective.

## How to implement
1. `Clinical/Treatments.cs`: `TreatmentDef` from `medicines.csv` + `content/treatments.json` (classes, not doses).
2. `RequiredChecks.cs`: each check is an action or derived state; missing → item (T-TR4).
3. Prerequisites (IV access for IV treatments → reject, T-TR5).
4. Effects referenced by ID in `content/physio/effects.json` (p0-physiology-events-behaviour-s05).
5. Validator rule DOSE_REQUIRES_REVIEWER: any `dose_display` without reviewer → content error (T-TR1).
6. openFDA contraindication/warning text curated for review only (`tools/content/import_labels.py`) — never doses.
7. Each treatment lists its required checks explicitly in content (no implicit checks in code).

## Verification
`dotnet test --filter FullyQualifiedName~Treatments` → T-TR1, T-TR4, T-TR5 green.

## Files changed
`src/ProcSense.Core/Clinical/Treatments.cs`, `RequiredChecks.cs`, `content/treatments.json`, `tools/content/import_labels.py`, tests.

## Dependencies
p0-medical-research-data-s06.

## Common issues
Label data is verbose — curate only needed fields.

## Sources
B-M14 Steps 1–4, T-TR1/4/5; walkthrough Part 8; inventory D24, D234–D267.
