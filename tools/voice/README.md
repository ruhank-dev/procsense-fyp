# Voice tools

- `catalogue.py` (`task voice-catalogue`): the list of every spoken line, built from the content (p0-voice-audio-s01, s06).
- `render.py` (`task voice-render`, `task voice-check`): speaks each plain line with an offline voice and writes `build/voice/<line id>.ogg` (git-ignored) and `build/voice/index.json` (p0-voice-audio-s02).
- `voices.json`: the voice and, per role, pitch and pace. `pronunciations.json`: respellings for words the voice says wrongly (the subtitle keeps the original text).

## One-time setup on a computer (nothing here is installed by `task setup`)

1. **Piper**, the archived MIT release, as a separate program: download `piper_windows_amd64.zip` from <https://github.com/rhasspy/piper/releases/tag/2023.11.14-2> and extract it to `build/voice-tools/piper/` (so `build/voice-tools/piper/piper/piper.exe` exists). SHA-256 of the archive recorded on 17 Oct 2026: `f3c58906402b24f3a96d92145f58acba6d86c9b5db896d207f78dc80811efcea`.
2. **The voice** `en_GB-cori-high` (LibriVox recordings, public domain, trained from scratch): download `en_GB-cori-high.onnx` and `en_GB-cori-high.onnx.json` from <https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_GB/cori/high> into `build/voice-tools/`. SHA-256 of the `.onnx`: `470b4dd634c98f8a4850d7626ffc3dfc90774628eeef6605a6dd8f88f30a5903`.
3. **ffmpeg**: `winget install Gyan.FFmpeg` (found automatically), or put it on PATH, or set `FFMPEG_EXE`. `PIPER_EXE` and `PIPER_MODEL` override the Piper and voice paths.

Then `task voice-render` (about 4 minutes the first time, seconds after) and `task voice-check`.

## Limits worth knowing

- Only this one voice is allowed by its licence so far (DT-184), so the roles are told apart by pitch and pace made from it. A man's lines are the same voice lowered; that is a stand-in until a second licensed voice exists. Nobody has listened to the result yet (EXP-VOX-1 is a listening test with people).
- Templates (lines with `{placeholders}`) and lines whose words the content does not give yet (DT-178 to DT-180) are not rendered; `index.json` lists both.
- Piper stays a separate program (its successor is GPL); ffmpeg likewise (licences: `docs/research/licence_provenance_register.md`).
