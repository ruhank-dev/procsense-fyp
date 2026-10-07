# p0-voice-audio

**Tier:** P0 (speech input + live TTS in p1-voice-input) · **Builds on:** p0-vr-platform · **Feeds:** characters (lip sync), scenarios

## Goal
Patients and staff speak with pre-rendered offline voices, every line has subtitles, mouths move, rooms sound like a hospital,
and monitors alarm according to Core state.

## Scope
- **In:** voice-line catalogue (from walkthrough quoted lines + approved staff lines), offline TTS pipeline, subtitles, lip sync,
  ambience, alarm sounds tied to alarm states, sound licence register.
- **Out:** speech recognition, LLM dialogue.

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Voices | Pre-rendered offline TTS (Piper; per-voice licence check) | Live TTS; recorded actors | Offline, small | EXP-VOX-1 → Kokoro or recorded team voices |
| Lip sync | uLipSync (audio-driven) | Viseme baking | Free, any audio | Quest cost too high |
| Sounds | Licensed library sounds (Freesound per-sound licence) or recorded | Synthesised | Realism | — |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-VOX-1 | Piper vs Kokoro naturalness | Blind listening test | Preferred engine chosen |
| EXP-VOX-2 | Alarm realism | Medical reviewer | Accepted |

## Exit
All P0 lines voiced + subtitled; alarms correct per state; every sound/voice licence recorded.

## Who does what
Claude: catalogue, pipeline, Unity audio scripts, licence checks. Team: listening tests.

## Sources
Implementation Guide B-S4, B-M9; inventory D65–D66, D81–D82.
