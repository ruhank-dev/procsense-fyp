# p1-backend-s06 — AI gateway endpoints (EXP-BE-2)

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
Gateway routers fronting the AI services: `/llm/generate`, `/dialogue/patient`, `/dialogue/staff`, `/behaviour`, `/classify`, `/debrief`, `/asr`, `/tts` — each with timeouts, the validator, audit, and fallback signals; placement: LLM on the RTX 4050 laptop, ASR/TTS/batch on the GTX 1660 Super desktop (B-R1 Step 7); EXP-BE-2 load test.

## Why
One controlled entry point for every AI capability means one place to enforce timeouts, validation, auditing and fallback — and lets the two GPUs (laptop + desktop) be used without the headset knowing where each model runs.

## How to implement
1. Routers per capability; shared `generate()` client.
2. Single-flight queue per GPU so concurrent requests don't thrash VRAM.
3. EXP-BE-2: concurrent LLM + ASR load test; record decision.
4. Each route: request/response schemas, timeout, validator, audit, fallback signal; `/health` reports per-service status.

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
