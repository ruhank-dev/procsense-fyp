# p0-simulation-workflow-s18 — Wristband mismatch and missing band: nurse resolution (engine)

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

Added 09 October 2026 at the project owner's request (the nurse workflow was in the source documents but in no step file — see [deferred-tasks.md](../../../../deferred-tasks.md) DT-006, DT-010, DT-011).
**Decision (owner, 09 October 2026):** the band is applied **instantly after verification**. Extending the same rule to the mismatch case (also instant in Core) is the agent's proposal — see "Design decision" below.

## What
The headless rules for fixing a wrong or missing wristband, so the trainee can recover instead of being stuck at `ST1_MISMATCH`:

1. **Mismatch reported** (`report_mismatch` while a real mismatch exists) → the nurse resolves it **in the same action**: the band shows the corrected identifiers (taken from the case's own
   authoritative record, never invented), the mismatch state clears, `identity_verified` stays false, and the trainee must **read the band again and re-confirm**
   (Walkthrough Part 11 Step 1: "Mismatch → stop; ask the nurse/admissions to resolve before anything else").
2. **Missing wristband** (case has no band on the wrist): `read_wristband` is rejected `WRISTBAND_MISSING` and `confirm_identity` stays unavailable. The new verb `request_wristband`
   is accepted **only after verification by other means** (open identity question asked, notes opened, spoken answer and notes agree — Walkthrough: "missing band → apply after verification");
   before that it is rejected `VERIFY_IDENTITY_FIRST`. When accepted, the band is applied **at once**. If the case has a reported allergy the allergy band is applied too, and the trainee must still ask for the reaction (rule L1-WT2).
3. **Nurse events for the views**: each resolution publishes `NurseTaskChanged` events in order (`CALLED → AT_BEDSIDE → REPLACING → DONE`) in the same Apply, so Unity can show the nurse walking in and replacing the band
   (p0-characters-s08) and the tablet can show a status chip. The events carry no time; the view paces the animation.
4. **Test-only fixture cases** (not among the 17 P0 cases): `TEST-ID-MISMATCH` (wristband DOB differs), `TEST-ID-MISSING-BAND`, `TEST-ID-MISSING-BAND-ALLERGY`, built from VAR-S1-A by mutation and marked test-only.
5. **Observable**: nurse task state and the replaced band appear in `ObservableState`. Band text still shows only after `read_wristband`.

## Design decision (proposal for the mismatch case)
The owner decided: instant after verification. Proposed for the whole workflow (change it if a timed nurse visit is preferred): **no simulated-time delay anywhere in this workflow.** Reasons: no invented timing numbers (no new register rows, no `content/staff.json`);
no timer state machine to keep deterministic and replayable; the learning point is "stop, verify, get it resolved", not waiting. The nurse still visibly walks in and replaces the band in VR, but that is **cosmetic pacing owned by the view**
(same rule as p0-characters-s06). Trade-off accepted: the simulation clock does not advance while the nurse works. Revisit if a case wants the wait to matter clinically (e.g. an S2 patient deteriorating) — then add a content-defined duration with a register row.

## Why
Rule L1-04 and test T01-4 (Implementation Guide §07) and the walkthrough both describe the nurse resolving a mismatch or applying a missing band. Step 6 only stops the trainee (`ST1_MISMATCH`);
without this step the safe path through a mismatch case does not exist.

## How to implement
1. `src/ProcSense.Core/Workflow/Steps/WristbandResolution.cs`: applies the resolution inside `OnAction` and publishes the `NurseTaskChanged` events on the EventBus (pipeline order `Staff`, or inside the identity subsystem if smaller).
2. Case schema + loader: allow a case to declare "no wristband on the wrist" (preferred: `wristband: null`, plus an optional `allergy_band` flag). Schema, `ContentLoader`, `tools/content/validate.py` updated together.
3. Verbs: `request_wristband`; `report_mismatch` now performs the resolution (today it only sets a flag). A repeated request when nothing is wrong is accepted with effect `nothing_to_resolve`.
4. Source conflict **ISS-0002 is closed** (owner: apply instantly after verification) — see `docs/research/issues.md`.
5. Tests: T01-3b (mismatch → resolved → re-read → confirm OK), T01-4 (missing band: rejected before verification, applied at once after), missing band + allergy, replay hash identical,
   a rejected action runs nothing else in the pipeline.
6. Golden scripts (pattern of p0-simulation-workflow-s04): `tests/golden/mismatch_resolved.actions.json`, `missing_band.actions.json`.

## Verification
`dotnet test` green; `task sim -- run …` on each fixture shows the nurse events in the log and a matching replay hash.

## Files changed
`src/ProcSense.Core/Workflow/Steps/WristbandResolution.cs` (+ identity subsystem edits), `content/schema/case.schema.json`, `src/ProcSense.Core/Content/*`, `tools/content/validate.py`, tests, `tests/golden/*`.

## Dependencies
p0-simulation-workflow-s01 (identity logic, done), p0-core-engine-s04 (observable/replay, done). Soft: p0-simulation-workflow-s13 (locations — where the nurse is).
Feeds: p0-characters-s08, p0-vr-interaction-ui-s12, p0-voice-audio-s06, p0-scoring-debrief-s01 (minor/critical items from the same flags).

## Common issues
- Reporting a mismatch when none exists is a minor "unnecessary delay" item for scoring, not a nurse visit (already the Step 6 behaviour — keep it).
- Do not let the corrected band reveal facts the trainee has not read: the observable shows band text only after `read_wristband`.
- The view must hold the old band text until its replace animation, otherwise the new text appears before the nurse reaches the wrist.

## Sources
Implementation Guide §07 rules L1-01…L1-04, tests T01-3, T01-4; Walkthrough Part 11 Step 1 (abnormal branches, "missing band → apply after verification"), equipment table (wristband, allergy band);
owner decision 09 October 2026; [deferred-tasks.md](../../../../deferred-tasks.md).
