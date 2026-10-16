# p0-voice-audio-s06 — Nurse lines for the wristband workflow

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

Added 09 October 2026 at the project owner's request ([deferred-tasks.md](../../../../deferred-tasks.md) DT-008).

## What
The spoken and subtitled lines of the nurse during a wristband mismatch or missing band, each with a catalogue ID, speaker, trigger event and source:

| Line (draft intent — final wording authored at implementation) | Trigger |
|---|---|
| Acknowledges the report, says she will check ("Let me check") | event `CALLED` |
| Greets at the bedside / asks to excuse her if the doorway is blocked | event `AT_BEDSIDE` |
| Says she is checking the records | event `AT_BEDSIDE` (second line) |
| Says she is replacing the band and what she found (procedural only) | event `REPLACING` |
| Confirms the new band is on and asks the trainee to check it | event `DONE` |
| Missing band: acknowledges and says she will apply a band | `request_wristband` accepted (after verification) |
| Missing band, asked too early: asks the trainee to verify identity first | rejection `VERIFY_IDENTITY_FIRST` |
| Missing band with allergy: mentions the allergy band | allergy case |
| Nothing to fix (request when the band is fine) | effect `nothing_to_resolve` |

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

## Implementation notes (16 Oct 2026)
- **Built:** `content/voice/nurse_wristband.json` with the nurse's 10 lines, each with an id, its trigger (the engine's nurse step CALLED, AT_BEDSIDE twice, REPLACING for a wrong band and for a missing one, DONE; `request_wristband` accepted; the rejection VERIFY_IDENTITY_FIRST; the effects `allergy_band_applied` and `nothing_to_resolve`) and its words, written procedural only ("the details on the band did not match the records"), with no clinical fact and nothing patient-specific; each `awaiting_review`. The voice catalogue (p0-voice-audio-s01) takes them in as nurse lines marked authored (310 lines now).
- **Missing-line check (T-VP1):** the catalogue reads the nurse's steps from the engine's code (`WristbandResolution`) and the outcomes this step lists, and reports any without a line, so a new nurse step cannot go silent. `tools/ci/test_voice_catalogue.py` (5 tests, in CI): every line has a source and an event, every P0 case has its patient lines, the nurse covers every step and outcome, a removed line is reported, and no nurse line holds a number or a placeholder.
- **Not built:** the review of the 10 lines (DT-181 now covers 24 drafts), and the rest of the step's plan that depends on later voice steps: rendering with the offline voice (p0-voice-audio-s02), subtitles and playback (s03), lip sync (s04) and the Play Mode scenario in which each nurse state plays its line. The step stays open until those exist.
