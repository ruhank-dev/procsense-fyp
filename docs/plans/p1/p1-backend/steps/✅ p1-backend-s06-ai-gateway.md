# p1-backend-s06 — AI gateway endpoints (EXP-BE-2)

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
Gateway routers fronting the AI services: `/llm/generate`, `/dialogue/patient`, `/dialogue/staff`, `/behaviour`, `/classify`, `/debrief`, `/asr`, `/tts` — each with timeouts, the validator, audit, and fallback signals; placement: LLM on the RTX 4050 laptop, ASR/TTS/batch on the GTX 1660 Super desktop (B-R1 Step 7); EXP-BE-2 load test. Every route requires a signed-in user (`p1-backend-s08`, `s10`), is rate-limited per organisation, and writes the audit row.

## Why
One controlled entry point for every AI capability means one place to enforce timeouts, validation, auditing and fallback — and lets the two GPUs (laptop + desktop) be used without the headset knowing where each model runs. A service hosted on the internet cannot reach models on a laptop behind a university network, so the gateway is built so it can run in two places: next to the models (on the team's machines, the current plan) or in the cloud later (OQ-07, DT-122). Both check the same login tokens, and when no model host is reachable a route answers with the fallback signal and the session carries on with the deterministic lines.

## How to implement
1. Routers per capability; shared `generate()` client.
2. Single-flight queue per GPU so concurrent requests don't thrash VRAM.
3. EXP-BE-2: concurrent LLM + ASR load test; record decision.
4. Each route: request/response schemas, timeout, validator, audit, fallback signal; `/health` reports per-service status.

## Status (17 October 2026)

- **Built:** `server/routers/ai_gateway.py`: `POST /llm/organisations/{id}/generate` (a registered prompt with `gateway: true` and a validator, signed-in member, rate limit, one model slot with a 5 s wait, timeout, schema and validator, one retry, the fallback signal, one audit row) and `GET /ai/health`; settings `OLLAMA_URL`, `AI_RATE_PER_HOUR`, `AI_QUEUE_WAIT_SECONDS`, timeouts; the server image carries `prompts/` and `content/ai/`. 11 tests on a real PostgreSQL with a fake model host (`server/tests/test_ai_gateway.py`). ADR-0011.
- **EXP-BE-2 (part):** `tools/ai/load_gateway.py`, 4 callers x 5 calls on the 4B: 16 answered, 4 got the fallback signal, queue wait p95 4.99 s, 71.8 calls a minute. The speech-recognition half could not be run.
- **Not built:** the routes `dialogue`, `staff`, `behaviour`, `classify`, `debrief`, `asr`, `tts`: each waits for its prompt and model (p1-ai-language, p1-voice-input).

## Verification
All AI routes visible and testable in `/docs`; EXP-BE-2 recorded.

## Files changed
`server/routers/ai/{llm,dialogue,staff,behaviour,classify,debrief,asr,tts}.py`, `server/schemas/ai/*`, `server/llm/client.py`, `docs/decisions/ADR-xxxx-ai-placement.md`.

## Dependencies
p1-backend-s05.

## Common issues
Two heavy models on one 6 GB GPU thrash VRAM → single-flight queue per GPU, ASR/TTS on the desktop.

## Sources
Implementation Guide B-R1 Steps 3, 6–8; B-M10; Master Plan §26 (where each model runs).
