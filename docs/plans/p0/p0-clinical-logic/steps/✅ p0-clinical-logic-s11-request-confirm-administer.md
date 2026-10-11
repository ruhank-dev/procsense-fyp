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

## Implementation notes (11 Oct 2026)
- **Code:** `Clinical/MedicineWorkflow.cs` holds `MedicineWorkflowSubsystem` (pipeline order `Treatments`, listed in `Subsystems.Default`). Trainee verbs: `request_medication(class, indication)`, `request_fluids(type)`, `request_oxygen(device)`, `request_iv_access`, plus `check_treatment_timing(treatment)` and `document_treatment(treatment)` (the timing and documentation checks of s10 are actions). There is no verb that gives anything; `npc_confirm`, `npc_administer` and `npc_challenge` are not verbs at all, the clinician and nurse are the engine (T-K8). Their steps are published on the EventBus as `MedicineEvent` and the effect ids as `TreatmentGiven`, and kept in `Session.MedicineHistory` with the score items.
- **Flow:** a request becomes an order (REQUESTED); after the clinician's delay it is CONFIRMED (the dose line appears now, never earlier); after the nurse's delay it is ADMINISTERED, or CHALLENGED. The delays (60 s and 60 s) are design values in `content/treatments.json`, section `workflow` (register row SR-4107). Times are counted from when each step was due, so one long tick, many short ticks and a time-skip give the same history. The nurse waits while the patient is in another place. `request_*` for an IV treatment without IV access is refused `IV_ACCESS_REQUIRED` (T-TR5); an order for the same treatment cannot be placed twice while one is waiting (`ORDER_PENDING`).
- **Dose display:** strictly by dose policy, from content: an educational example shows the label, the text and the source; a patient-specific one shows the label and "the clinician decides" and never a digit; an institutional one shows its label (tests check the lines of every treatment).
- **Allergy and other critical problems (T-TR3):** judged on the chart. A medicine in an allergy group (antibiotics: the words penicillin and antibiotic) for a patient whose recorded reaction is an immediate severe one (throat swelling, collapse, anaphylaxis) is a conflict; a mild old reaction is not, but the clinician's line notes it. A conflict, an applying restriction or allergies not yet established is handled by mode, from content (`allergy.outcome`, register row SR-4108): practice = the nurse challenges and does not give (the line comes from the approved list, a time-skip stops), assessment = it is given, the problem is a critical score item and, for an allergen, `allergy.reaction_eligible` is set and `WorkflowEvent("E-HYPERSENSITIVITY")` is published (the event director draws it with the seeded stream when it exists). Practice and assessment stand in for the beginner and advanced settings until the difficulty setting exists (DT-056).
- **Restrictions:** the patient's conditions are the session flags named `condition.<tag>` (for example `condition.kidney_injury`). Nothing sets them yet; the three drug checks of s12 and the physiology step will.
- **Observable:** `ObservableState.Orders` (id, treatment, label, state, device, dose, lines). It is written to the canonical JSON only once an order exists, so every existing golden hash is unchanged.
- **Content:** `build_treatments.py` now also writes `workflow`, `oxygen_devices`, `allergy`, `dose_labels` and `lines`, each checked against the rows it rests on, and register rows SR-4107 to SR-4109.
- **Tests:** `MedicineWorkflowTests` (28): T-TR2, T-TR3 (both modes), T-K8, delays, tick-size independence, dose lines, IV rule, oxygen devices, challenges, restrictions, documentation, replay.
- **Not in this step:** no indication rule (any reason text counts as a recorded reason; whether it is the right reason is the decision engine's job, B-R17); the kidney, liver, pregnancy and blood-thinner checks and the clash table (s12); the physiology that uses the effect ids.
