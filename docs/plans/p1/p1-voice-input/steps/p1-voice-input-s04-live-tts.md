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
