# p1-theatre-extension-s02 — Intra-operative training events (time-lapse)

Milestone: **M11 P1 content — theatre extension, extended cases, composer, Pulse**

## What
The procedure is abstracted as a time-lapse (the operation itself is out of scope — walkthrough 2.3) with clinician-approved, clearly labelled 'ProcSense educational extension' events: e.g. adhesions → surgeon asks for the open set (trainee communicates/fetches), bleeding needing suction, unexpected findings (unexpected stone / normal-looking appendix).

## Why
Teamwork moments during surgery (asking for the open set, managing bleeding with suction, unexpected findings) are part of safe theatre practice the walkthrough extends to — without simulating the operation itself.

## How to implement
1. Events as data (Type D teaching devices, each requiring clinician validation) from walkthrough C.22–C.24 / F,G stages 14–17.
2. Time-lapse via Core time-skip; event cards in the OT; freezing (no action) → debrief item.
3. Event cards show 'ProcSense educational extension' visibly; each needs reviewer approval before it can appear.

## Status (17 October 2026)

- **Built:** `src/ProcSense.Core/Workflow/Steps/IntraOpSubsystem.cs` and `content/extension/intra_op_events.json` (built by `tools/content/build_intraop_events.py`, SR-4131): after the Time Out `intraop_begin` plays the time-lapse and one of four labelled events (adhesions and the open set, bleeding that needs suction, an unexpected finding, a normal-looking appendix) is chosen by the session seed from those that apply to the scenario; the trainee answers with `intraop_respond` within six simulation steps; a teamwork answer is credited, a wrong one is a minor item, freezing is a major item; practice mode only; every event carries the visible label 'ProcSense educational extension' and awaits a clinician.
- **Verification result:** `TheatreExtensionTests` (11 tests) cover the waits, the credit, the wrong answer, the freeze, the seed and the refusal in assessment mode. Not built: the Unity event cards (Sannan) and the clinician's review of each event.

## Verification
T-EXT1-style tests per event; reviewer approves each event.

## Files changed
`content/extension_events.json`, OT event-card UI, time-lapse controller, tests.

## Dependencies
p1-theatre-extension-s01.

## Common issues
Every event visibly labelled as an extension.

## Sources
B-S12 Steps 1–2; walkthrough Part C.22–C.24, F/G 14–17.
