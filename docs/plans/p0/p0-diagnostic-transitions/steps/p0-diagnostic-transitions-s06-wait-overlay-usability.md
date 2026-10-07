# p0-diagnostic-transitions-s06 — Wait overlay, notifications and usability (EXP-DT-1)

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
During a B-room visit: a small overlay shows the patient's location and the sim-clock advance (~10 s on screen, configurable in content); a notification when results are ready; the trainee can keep working on other tasks.

## Why
When the patient disappears through a door, trainees must always know where the patient is, roughly when results will be back, and that they can (and in S2 should) keep working meanwhile.

## How to implement
1. `Scripts/Diagnostics/WaitOverlay.cs`; wait length from `content/diagnostics.json`.
2. EXP-DT-1 with testers (no instructions).
3. Overlay shows: patient location ('In CT'), expected back time on the sim clock, and a fast-forward indicator while time-skips.
4. Notification: tablet + EMR badge + nurse line 'Results are back' when ResultReady fires.

## Verification
EXP-DT-1 result in the README; changing the wait length in `content/diagnostics.json` changes the on-screen wait without code changes.

## Files changed
UI, `content/diagnostics.json`, results notes.

## Dependencies
p0-diagnostic-transitions-s03.

## Common issues
Too short a wait feels instant and unrealistic; too long is boring — keep it configurable and decide with testers (EXP-DT-1).

## Sources
p0-diagnostic-transitions README decisions + EXP-DT-1; walkthrough Part 17 (turnaround times).
