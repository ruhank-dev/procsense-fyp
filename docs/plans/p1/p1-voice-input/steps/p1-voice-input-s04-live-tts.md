# p1-voice-input-s04 — Live TTS for AI-generated lines

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
Server-side TTS for lines the LLM generates at runtime: the same voice per character as the offline pipeline where possible, sentence-level streaming (first sentence sent as soon as it's ready), caching of repeated lines, and client playback with lip sync; when offline, subtitles only.

## Why
AI dialogue without a voice breaks immersion; mismatched voices between pre-rendered and live lines are jarring.

## How to implement
1. `/tts` route; sentence split; caching of repeated lines.
2. Same voice per character as the offline pipeline where possible; cache repeated lines server-side.
3. Sentence splitter on the LLM output; first sentence synthesised immediately, the rest streamed.
4. Client falls back to subtitles if TTS times out (2.5 s).

## Status (17 October 2026)

- **Built:** `server/tts/engine.py` (Piper as a separate program kept alive, one throw-away sentence at start so the voice is loaded once, ffmpeg shaping, the roles, pitch and pace of the offline voices from `tools/voice/voices.json`, one loudness pass, Ogg Vorbis, sentence by sentence, an LRU cache) and `POST /tts/organisations/{id}/speak` (newline-delimited JSON, one sentence per line; audited; rate limited; the fallback signal when the voice is unavailable or the feature is off). 9 tests.
- **Fixed on 17 October:** the engine read Piper's WAV from standard output; on Windows the text-mode stream turned every line-feed byte into two and the speech was garbled (the recogniser heard "I love you"). It now gives Piper a file; a test pins it.
- **Measured** (`tools/ai/tts_latency.py`, three runs of the first sentence of 12 catalogue lines, a different role each run): first spoken sentence **2.19 s at the 95th percentile** (worst of the runs; median 1.37 s) against the 2.5 s budget, so the `tts` gate passes and the feature is on. Baselines kept in `reports/ai/tts-latency.json`: 6.7 s with Piper started afresh for every sentence, 4.95 s with a kept-alive Piper while language-model evaluations were running. Profile on a quiet processor: Piper about 0.2 s per word (the "high" voice), ffmpeg shaping 0.1 to 0.2 s.
- **Limit:** the figure holds on a quiet processor; it is not guaranteed while the 9B model or the recogniser runs on the same machine (the split topology of `docs/architecture/deployment.md` keeps them apart). The first sentence of a long line is the unit measured; a one-word line is faster, a twelve-word first sentence is not.

## Verification
First sentence plays within the latency budget; voices consistent with pre-rendered lines.

## Files changed
`server/routers/tts.py`, `server/tts/engine.py`, voice mapping reused from `tools/voice/voices.yaml`, Unity `Scripts/Audio/StreamingVoice.cs`.

## Dependencies
p1-ai-language-s04.

## Common issues
Live TTS on the same GPU as the LLM can stall replies — place TTS on the desktop (B-R1 placement).

## Sources
Implementation Guide B-R3 Step 4 (streaming), B-R1 Step 7 (placement), B-M9 (voices); inventory D65–D66.
