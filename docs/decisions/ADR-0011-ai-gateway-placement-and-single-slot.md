# ADR-0011 — The AI gateway runs next to the models and lets one model call run at a time

**Date:** 17 October 2026 · **Status:** Accepted (17 October 2026, implemented under the owner's instruction to build the P1 plan; the placement of the live model on the laptop is still open, DT-188) · **Related:** ADR-0005, ADR-0009, ADR-0010, p1-backend-s05, p1-backend-s06, EXP-BE-2

## Context
Language-model features need one controlled door: a signed-in user, a rate limit, a timeout, a check of every answer, an audit row and a fallback when the model is down. The hosted service (Render and Supabase, ADR-0009) cannot reach a model on a laptop behind a university network. The desktop GPU has 6 GB: two heavy models at once thrash it. Measured on 17 October 2026 with `tools/ai/load_gateway.py`: 4 callers, 5 calls each, the 4B model, one slot, a 5 second wait for the slot: 16 calls answered, 4 got the fallback signal, queue wait p50 1.4 s and p95 5.0 s, service time p50 0.5 s and p95 9.5 s, 72 calls per minute. The speech-recognition half of EXP-BE-2 could not be run (no speech recognition is installed yet).

## Decision
1. The gateway (`server/routers/ai_gateway.py`, `server/llm/`) is part of the service and is configured with the address of the model host (`OLLAMA_URL`, default `127.0.0.1:11434`). It runs where the models run (the team's machines); a cloud copy with no host answers every call with the fallback signal and the session carries on with deterministic lines.
2. One model call at a time per host (a single slot). A request waits up to `AI_QUEUE_WAIT_SECONDS` (5 s) for it and then gets the fallback signal; it never queues without limit.
3. A prompt can be called through the gateway only if it is registered (`prompts/registry.json`) with `gateway: true` and has a validator (`server/llm/validators.py`). Free-form prompts do not exist. The answer passes its JSON Schema and the validator before it leaves; one retry carries the reasons; then the fallback signal.
4. Every call writes one audit row (hashes, ids and reason codes, never text). Rate limit per person (`AI_RATE_PER_HOUR`, 600).
5. Offline batch work (composition, narrative, interpretation) uses the same client and gateway function from tools and the pool builder, not the HTTP routes.

## Alternatives
A model host in the cloud (breaks offline use and puts personal data on a third party; kept as an option for offline batch jobs only, never personal data); one model call per GPU plus a queue with no limit (a trainee would wait unseen); a route per capability with free text prompts (no registry, no single point of check).

## Consequences
Every AI feature is a registered prompt plus a validator; adding one is a data change. Under load the user sees the fallback line rather than a delay. The routes `dialogue`, `staff`, `behaviour`, `classify`, `debrief`, `asr` and `tts` of p1-backend-s06 wait for their prompts and models (p1-ai-language s04, s05, s07, s08 and p1-voice-input); `/llm/.../generate` and `/ai/health` are built. The server image carries `prompts/` and `content/ai/` (a test checks the Dockerfile).

## Revisit if
The live model moves to the laptop and the service to the cloud (then the gateway must reach it over a tunnel, DT-122); two models fit on one GPU; the fallback rate under real load is above a few percent.
