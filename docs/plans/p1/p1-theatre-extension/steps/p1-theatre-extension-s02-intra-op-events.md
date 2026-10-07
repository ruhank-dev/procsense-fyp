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
