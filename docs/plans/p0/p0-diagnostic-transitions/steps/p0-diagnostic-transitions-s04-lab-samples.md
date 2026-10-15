# p0-diagnostic-transitions-s04 — Lab samples via porter or tube station

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
Lab tests don't move the patient: after bedside sampling and labelling, the trainee places the tubes in the specimen drop / pneumatic-tube station (or hands them to the porter); samples travel to the Laboratory door; results return after the test's turnaround; unlabelled or pre-labelled tubes trigger the walkthrough's rejection / wrong-blood consequences.

## Why
Taking blood after the identity check and labelling at the bedside is a core walkthrough safety lesson; sending samples (not the patient) to the lab keeps that lesson intact inside the door-transition design.

## How to implement
1. Socket on the tube station emits `send_samples(tubes[])`; optional porter pickup animation.
2. Core validates labels (p0-clinical-logic-s07).
3. Station accepts only labelled tubes; unlabelled tube → returned with a lab rejection message after a delay (Core decides).
4. Porter route (optional) uses the same PatientMover agent type without a patient.

## Verification
Play Mode test: labelled LFT tube → result after turnaround; unlabelled → rejection message and re-take required.

## Files changed
`unity/Assets/_Project/Scripts/Diagnostics/TubeStation.cs`, tube-station prefab (Blender asset from p0-blender-equipment-s08), `content/diagnostics.json`, Play Mode tests.

## Dependencies
p0-diagnostic-transitions-s01.

## Common issues
Tubes left on the bed table never get sent — the nurse can prompt in practice mode (beginner safety net), not in assessment.

## Sources
p0-environment-rooms README clinical note 1; Implementation Guide §12 (L6-02, E6-B), B-M13 Step 4; walkthrough Step 6.

## Implementation notes (15 Oct 2026): the Core half
- **Built in Core:** `take_sample` with `send=later` keeps a blood sample at the bedside (state SAMPLE_TAKEN, trip stage SAMPLE_WAITING); the new verb `send_samples` (via `tube_station` or `porter`, optionally `tests=lft,fbc`) sends every waiting tube or the named ones (state SENT); the laboratory's turnaround starts only when the sample arrives (tube station 2 minutes; porter: the porter arrival time of the locations, 2 to 4 minutes drawn from its own seeded stream, then 5 minutes). The laboratory still checks each tube as before (p0-clinical-logic-s07): an unlabelled tube comes back REJECTED with the rejection message and must be ordered and taken again; a pre-labelled one gives someone else's result. In practice mode at the beginner level the nurse points out tubes still at the bedside after 10 minutes (`SamplesWaiting` event, once per sample, and a time-skip stops there); never at the advanced level or in assessment. `SampleSent` now names how the sample went.
- **Unchanged on purpose:** without `send`, `take_sample` sends at once by the porter exactly as before, so every recorded session, the golden runs (24 of 24 unchanged) and today's Unity scenes keep working.
- **Content:** `content/diagnostics/diagnostics.json` (`tools/content/build_diagnostics.py`, checked in CI, including that every register row it cites exists; the same check was added to `build_records.py`): the two ways, their transit times, the prompt's delay and wording; design values SR-4120 to SR-4122 in a new hand-written register range SR-4120 to SR-4129 (provisional, not clinically reviewed).
- **Tests:** `SampleDispatchTests` (8): the content, the old path unchanged, the tube station, the porter carrying every waiting tube, named tubes and every refusal (UNKNOWN_DISPATCH, NOT_WAITING, NOTHING_TO_SEND, BAD_SEND), an unlabelled tube rejected and re-taken, the beginner-only prompt, a skip stopping at the prompt. Unity batch tests 149 of 149.
- **Not built (the Unity half, Sannan):** `TubeStation.cs` with a socket that calls `send_samples` for the tubes placed in it, the tube-station prefab (p0-blender-equipment-s08), the optional porter walk, the nurse's line for `SamplesWaiting`, and the Play Mode test (labelled tube to a result, unlabelled to a rejection). The step stays open until that half is done.
- **Core half approved by the owner on 15 Oct 2026** (decision row 83); the step file stays unticked until the Unity half is done.
