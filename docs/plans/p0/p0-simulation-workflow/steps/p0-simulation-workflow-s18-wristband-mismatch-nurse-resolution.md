# p0-simulation-workflow-s18 — Wristband mismatch and missing band: nurse resolution (engine)

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

Added 09 October 2026 at the project owner's request (the nurse workflow was in the source documents but in no step file — see [deferred-tasks.md](../../../../deferred-tasks.md) DT-005, DT-009, DT-011).

## What
The headless rules for fixing a wrong or missing wristband, so the trainee can recover instead of being stuck at `ST1_MISMATCH`:

1. **Mismatch reported** (`report_mismatch` while a real mismatch exists) → a **nurse task** `WRISTBAND_REPLACEMENT` starts and moves through
   `DISPATCHED → ARRIVED → CHECKING_RECORDS → REPLACING → DONE`. Each state change is a Core event (`NurseTaskChanged`) so Unity can animate it
   (the nurse enters from outside — see p0-characters-s08).
2. **Replacement**: when the task reaches `REPLACING → DONE` the wristband text becomes the corrected identifiers (taken from the case's own
   authoritative record, never invented). `identity_verified` stays false; the mismatch state clears; the trainee must **read the band again and
   re-confirm** (Walkthrough Part 11 Step 1: "Mismatch → stop; ask the nurse/admissions to resolve before anything else").
3. **Missing wristband** (case has no band on the wrist): `read_wristband` is rejected `WRISTBAND_MISSING`, `confirm_identity` stays unavailable, and a new
   verb `request_wristband` starts the same nurse task, which applies a new band after verification (Walkthrough: "missing band → apply after verification").
   If the case has a reported allergy, the nurse also applies the allergy band and the trainee must still ask for the reaction (rule L1-WT2).
4. **Test-only fixture cases** (not among the 17 P0 cases): `TEST-ID-MISMATCH` (wristband DOB differs), `TEST-ID-MISSING-BAND`,
   `TEST-ID-MISSING-BAND-ALLERGY`, built from VAR-S1-A by mutation and marked test-only.
5. **Observable**: nurse task state ("nurse called", "nurse at bedside", "band replaced") appears in `ObservableState` so the tablet and Unity can show it.

## Why
Rule L1-04 and test T01-4 (Implementation Guide §07) and the walkthrough both describe the nurse resolving a mismatch or applying a missing band. Step 6 only
stops the trainee (`ST1_MISMATCH`); without this step the safe path through a mismatch case does not exist.

## How to implement
1. `content/staff.json` (new, with register rows, evidence type D): nurse response delay, check time, replacement time (simulator design values — the
   owner accepted design values of this kind; each still gets a register row).
2. `src/ProcSense.Core/Workflow/Steps/WristbandResolution.cs`: the nurse task state machine, advanced in `OnTick` from the sim clock (no wall clock),
   publishing `NurseTaskChanged` on the EventBus. Uses the same chain-of-responsibility pipeline; extends the identity subsystem or sits beside it at
   `PipelineOrder.Staff`.
3. Case schema + loader: allow a case to declare "no wristband on the wrist" (preferred: `wristband: null`, plus an optional `allergy_band` flag).
   Schema, `ContentLoader`, `tools/content/validate.py` updated together.
4. Verbs: `request_wristband`; `report_mismatch` now starts the task (today it only sets a flag). Duplicate requests while a task runs are accepted with
   an effect `already_on_the_way`, not a second nurse.
5. Open source conflict to record first: walkthrough "apply after verification" vs Guide T01-4 "nurse applies band after delay, identity then confirmable"
   → add **ISS-0002** to `docs/research/issues.md`; implement the walkthrough (master source) and cover both orders in tests.
6. Tests: T01-3b (mismatch → nurse → replaced → re-read → confirm OK), T01-4 (missing band), missing band + allergy, request while a nurse is already
   coming, replay hash identical, nothing else in the pipeline runs for a rejected action. The weak-point test: a long idle wait must not skip the nurse states.
7. Golden scripts (feeding p0-simulation-workflow-s04's pattern): `tests/golden/mismatch_resolved.actions.json`, `missing_band.actions.json`.

## Verification
`dotnet test` green; `task sim -- run …` on each fixture shows the nurse states in the observable and a matching replay hash.

## Files changed
`src/ProcSense.Core/Workflow/Steps/WristbandResolution.cs` (+ identity subsystem edits), `content/staff.json`, `content/schema/case.schema.json`,
`docs/research/source_register.csv`, `docs/research/issues.md`, tests, `tests/golden/*`.

## Dependencies
p0-simulation-workflow-s01 (identity logic), p0-core-engine-s04 (observable/replay). Soft: p0-simulation-workflow-s13 (locations — where the nurse is).
Feeds: p0-characters-s08, p0-vr-interaction-ui-s12, p0-voice-audio-s06, p0-scoring-debrief-s01 (minor/critical items from the same flags).

## Common issues
- The nurse delay must come from content and the sim clock, never from Unity animation length.
- Reporting a mismatch when none exists is a minor "unnecessary delay" item for scoring, not a nurse visit (already the Step 6 behaviour — keep it).
- Do not let the corrected band reveal facts the trainee has not read: the observable shows band text only after `read_wristband`.

## Sources
Implementation Guide §07 rules L1-01…L1-04, tests T01-3, T01-4; Walkthrough Part 11 Step 1 (abnormal branches, "missing band → apply after verification"),
equipment table (wristband, allergy band); [deferred-tasks.md](../../../../deferred-tasks.md).
