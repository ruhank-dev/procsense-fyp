# p1-ai-language-s02 — LLM serving and benchmark (EXP-AI-1)

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
Ollama on the RTX 4050 laptop; candidate models qwen3:8b (Q4_K_M), qwen3:4b, a medical model (MedGemma 4B — accept its terms first); measure VRAM at num_ctx 2048/4096/8192 (keep ≥ 400 MB headroom); structured output via schema + re-validation; benchmark script over tasks × models (p50/p95 latency, tokens/s, VRAM, JSON validity, grounding failures); GPU tuning (smallest num_ctx that fits, keep_alive 30 m + warm-up, num_predict caps per task); guard-rail system prompts (educational simulation, trainee never prescribes, no doses except supplied example text); ADR.

## Why
6 GB VRAM makes model choice an experiment, not a guess.

## How to implement
1. Install + pin versions in `docs/AI_SETUP.md`.
2. `server/llm/client.py` `generate(task, schema_id, prompt_id, context_id, messages)`; prompts in `prompts/<task>/vN.txt`.
3. `tools/ai_eval/bench_llm.py` — 200 prompts per task → CSV.
4. ADR with benchmark evidence.

## Verification
T-LLM1 100% schema-valid after retry; T-LLM2 p95 ≤ 1.6 s LLM (≤ 2.5 s end-to-end); T-LLM3/4/6 pass; ADR written.

## Files changed
`server/llm/*`, `prompts/*`, `tools/ai_eval/bench_llm.py`, `docs/AI_SETUP.md`, ADR.

## Dependencies
p1-ai-language-s01.

## Common issues
OOM on the laptop → close the Unity Editor, lower num_ctx.

## Sources
Implementation Guide B-R1 Steps 1–9, T-LLM1–T-LLM6.
