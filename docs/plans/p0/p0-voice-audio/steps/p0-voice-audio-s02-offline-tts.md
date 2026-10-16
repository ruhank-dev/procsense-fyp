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

## Status (17 October 2026)

- **Built:** `tools/voice/render.py` (`task voice-render`, `task voice-check`), `voices.json`, `pronunciations.json`, `tools/voice/README.md` (setup), tests `tools/ci/test_voice_render.py` in CI. Piper (archived MIT release, run as a separate program) speaks each line once (cached by its words), ffmpeg shapes the pitch and pace per role, applies the exact gain to -16 LUFS with a -1.5 dB limiter and writes Ogg Vorbis. 282 plain lines (280 distinct ids after fixing a duplicate id in the catalogue), 669 seconds of speech, all within 1.5 LU of -16 LUFS (T-VP2 by machine). Output is in `build/voice` (git-ignored); copying it into the Unity project is the playback step (s03) and the question of committing audio or rebuilding it follows EXP-FND-1.
- **Not built / not claimed:** EXP-VOX-1 (Piper against Kokoro, a blind listening test by people; DT-194); a second voice (DT-184, licence); 9 template lines (their value is only known in a session, DT-195); 19 lines with no words yet (DT-178 to DT-180). Nobody has listened to the rendered audio, so naturalness, the pitch-shifted "male" patient voices and the respelling dictionary are unchecked.
- **Catalogue fix found here:** VAR-S1-D's two variant answers shared the id of its base medicines answer; each variant line now has its own id (`PAT-VAR-S1-D-MEDICINES-D1`, `-D2`), and a test keeps the ids unique.

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
