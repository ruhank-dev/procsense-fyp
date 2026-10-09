# p0-voice-audio-s06 — Nurse lines for the wristband workflow

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

Added 09 October 2026 at the project owner's request ([deferred-tasks.md](../../../../deferred-tasks.md) DT-008).

## What
The spoken and subtitled lines of the nurse during a wristband mismatch or missing band, each with a catalogue ID, speaker, trigger event and source:

| Line (draft intent — final wording authored at implementation) | Trigger |
|---|---|
| Acknowledges the report, says she will check ("Let me check") | task `DISPATCHED` |
| Greets at the bedside / asks to excuse her if the doorway is blocked | `ARRIVED` |
| Says she is checking the records | `CHECKING_RECORDS` |
| Says she is replacing the band and what she found (procedural only) | `REPLACING` |
| Confirms the new band is on and asks the trainee to check it | `DONE` |
| Missing band: acknowledges and says she will apply a band | `request_wristband` accepted |
| Missing band with allergy: mentions the allergy band | allergy case |
| Already on the way (duplicate request) | `already_on_the_way` |

## Why
Without voice and subtitles the nurse's arrival is silent and the trainee cannot tell what is happening; lines also give the Core a place to teach ("stop and resolve before anything else").

## How to implement
1. Add the lines to the voice-line catalogue generator input (`content/voice/…`), speaker `NURSE`, `spoken_variant: authored`, flagged for review (the lines are procedural, no clinical values).
2. Render with the offline TTS (p0-voice-audio-s02); subtitles and playback via s03; lip sync via s04 for the nurse avatar.
3. Missing-line check: every nurse task state in p0-simulation-workflow-s18 must have a catalogue line (build warning T-VP1 otherwise).
4. Subtitles are always shown (hearing accessibility) and match the audio text exactly.

## Verification
Catalogue build has no warnings; in the Play Mode scenario each nurse state plays its line with subtitles.

## Files changed
`content/voice/*`, catalogue tool input, rendered audio, tests.

## Dependencies
p0-voice-audio-s01…s04, p0-simulation-workflow-s18, p0-characters-s08.

## Common issues
Do not put clinical facts in nurse lines (what she "found" must be generic — "the details on the band did not match your records"); anything patient-specific comes from Core data.

## Sources
Guide L1-04 feedback column ("Nurse line"); Walkthrough Part 11 Step 1; owner instruction 09 October 2026.
