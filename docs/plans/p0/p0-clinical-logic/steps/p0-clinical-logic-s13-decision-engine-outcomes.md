# p0-clinical-logic-s13 — Decision engine: five outcomes and the master decision

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
DecisionOutcome {SAFE, INVESTIGATE, OPTIMISE, REVIEW, EMERGENCY} re-evaluated whenever information changes, encoding the walkthrough Step 8 diagram as ordered checks: emergency sign → different/complicated diagnosis → fixable problem → anaesthesia-affecting → safe. Scenario policy: S1 fixable problem → usually OPTIMISE (defer/re-time = success); S2 → correct in parallel, proceed when life threats are treated and blockers resolved.

## Why
The central skill: 'Is it safe to continue?' asked again after every new fact.

## How to implement
1. `Clinical/DecisionEngine.cs`: ordered checks driven by `decision_rules.csv` + problem registry (s14) + calculators; output expected outcome + reason + evidence refs.
2. Re-evaluate on state changes that touch the rules' keys (reuse the dirty-key idea from the case graph).
3. Expose expected outcome to scoring only (never shown to the trainee in assessment).
4. Tests per P0 case decision point (T-CG7 VAR-S1-PB; T-CG8 VAR-S2-B EMERGENCY then REDIRECTED).

## Verification
`dotnet test --filter FullyQualifiedName~DecisionEngine` green for every P0 decision point.

## Files changed
`src/ProcSense.Core/Clinical/DecisionEngine.cs`, tests.

## Dependencies
p0-clinical-logic-s14; p0-medical-research-data-s05.

## Common issues
Compute expected, compare with the trainee's declaration — never decide for them.

## Sources
B-R12 Step 9, B-R17; walkthrough Part 3, Step 8 diagram; inventory D187–D205.
