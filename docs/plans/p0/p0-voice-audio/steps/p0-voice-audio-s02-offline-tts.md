# p0-voice-audio-s02 — Offline TTS rendering (EXP-VOX-1)

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

## What
Render the catalogue with an offline TTS engine run as an external tool (Piper voices; compare Kokoro in EXP-VOX-1), one voice per character (`voices.yaml`), batch on CPU, loudness-normalise to −16 LUFS (ffmpeg loudnorm), encode Ogg Vorbis, write the line → file index.

## Why
Pre-rendered voices make the simulator fully usable offline (no laptop needed), keep latency at zero, and let the reviewer hear every line before trainees do.

## How to implement
1. `tools/voice/render.py` (uv; parallel CPU workers) + `voices.yaml` (character → voice + terms).
2. Pronunciation overrides for drug/medical terms (respelling dictionary).
3. EXP-VOX-1 blind listening test (Piper vs Kokoro); record choice per role.
4. Output `unity/Assets/_Project/Audio/Voice/*.ogg` (LFS) + `catalogue.json` paths.

## Verification
T-VP2 all lines rendered; T-VP3 voice terms recorded; EXP-VOX-1 result recorded.

## Files changed
`tools/voice/*`, `voices.yaml`, audio files.

## Dependencies
p0-voice-audio-s01.

## Common issues
Piper is GPL — run it only as an external tool (never link it into the app). Mispronounced drug names → respelling dictionary. Re-render only changed lines (hash the text + voice).

## Sources
B-M9 Steps 1–3, T-VP2/3; inventory D65–D66.
