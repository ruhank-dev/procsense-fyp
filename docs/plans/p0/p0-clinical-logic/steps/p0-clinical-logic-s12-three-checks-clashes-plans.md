# p0-clinical-logic-s12 — Three drug checks, treatment clashes, medicine plans

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Every request runs the walkthrough's checks — allergy? kidneys/liver? pregnancy? (+ anticoagulant/bleeding risk) — and the clash table (14B.3) against patient factors (allergy, AKI, pregnancy, anticoagulant, QTc, diabetes); perioperative medicine plans (diabetes meds day-before/day-of, anticoagulant stop times, steroids, ACE-i, diuretics) become Step 8 decisions.

## Why
Stacked cases and several P0 variants (e.g. VAR-S1-C diabetes, VAR-S1-D apixaban) depend on these.

## How to implement
1. `Clinical/ClashChecker.cs` over `treatment_clashes.csv` (data, no pairwise code) → clashes[] for senior/anaesthetist review (T-DE2: NSAID + AKI).
2. `Clinical/DrugChecks.cs` from `drug_checks.json`.
3. Medicine-plan decision points per case medication (`medicines.csv` plan/timing; Type C where marked).
4. Tests from VAR-S1-C/C2 and VAR-S1-D(a/b) walkthrough outcomes.
5. Clash results feed the problem registry (clash = SAFETY_BLOCKER or REVIEW problem per content).

## Verification
`dotnet test --filter FullyQualifiedName~Clash` + `dotnet test --filter FullyQualifiedName~MedicinePlan` → T-DE2 green; VAR-S1-D(a) proceed, (b) this-morning dose → per walkthrough.

## Files changed
`src/ProcSense.Core/Clinical/ClashChecker.cs`, `DrugChecks.cs`, tests.

## Dependencies
p0-clinical-logic-s11; p0-medical-research-data-s06.

## Common issues
DOAC stop times are institutional (decision 5) — content only.

## Sources
B-M14 Steps 6–7, B-R17 Step 4, T-DE2; walkthrough Part 8, 14B.3; inventory D211, D241–D261.
