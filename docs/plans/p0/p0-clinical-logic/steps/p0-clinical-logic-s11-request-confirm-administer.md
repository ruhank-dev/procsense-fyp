# p0-clinical-logic-s11 — Medicine workflow: request → confirm → administer

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
The trainee requests a medicine class / fluid / oxygen; the clinician NPC confirms drug + dose (educational example text may be shown with its source, never as an instruction; patient-specific doses shown as 'clinician decides'); the nurse NPC administers — or challenges an unsafe request (beginner mode) / the reaction happens (advanced mode). There is no trainee administer verb.

## Why
Walkthrough Part 8.1 role convention and the safety statement: the trainee never prescribes, doses or administers.

## How to implement
1. Verbs: trainee `request_medication(class, indication)`, `request_fluids(type)`, `request_oxygen(device)`; NPC actions `npc_confirm`, `npc_administer`, `npc_challenge` emitted by the engine, not the trainee.
2. Pending-orders list in ObservableState; confirmation delay Type D.
3. Allergy conflict (T-TR3): beginner → nurse challenge line; advanced → administered + E-HYPERSENSITIVITY eligible (seeded) — walkthrough 14.4.
4. Dose display strictly by `dose_policy`.
5. Test T-K8: no `administer` verb registered for the trainee channel.

## Verification
`dotnet test --filter FullyQualifiedName~MedicineWorkflow` → T-TR2, T-TR3, T-K8 green.

## Files changed
`src/ProcSense.Core/Clinical/MedicineWorkflow.cs`, tests.

## Dependencies
p0-clinical-logic-s10.

## Common issues
Difficulty mode decides challenge vs reaction — from content, per case.

## Sources
B-M14 Step 5, B-R17 Step 5, B-R11 Step 9; walkthrough 8.1, 14.4.
