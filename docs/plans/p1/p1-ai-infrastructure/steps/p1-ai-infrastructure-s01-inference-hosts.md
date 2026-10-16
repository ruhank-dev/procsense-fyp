# p1-ai-infrastructure-s01 — Inference hosts: install and pin the runtimes on both machines

Milestone: **M16 P1 AI infrastructure and personalisation — inference hosts, dual-GPU benchmark, labelling, learner model**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

> **Status (16 October 2026).** Desktop part done with the owner's approval (the dependency below): Ollama 0.40.5 and the two Qwen3.5 models are installed and measured (ADR-0010); digests are in the licence register. Not done: the laptop, faster-whisper, the TTS engine, `tools/ai/check_hosts.py` and the health endpoint. This step stays open.

## What
Install and pin Ollama with the Qwen3.5 tags (`qwen3.5:4b-q4_K_M` and `qwen3.5:9b-q4_K_M`; the 0.8B and 2B only for the benchmark), faster-whisper (small.en, medium.en) and a TTS engine on the desktop (GTX 1660 SUPER) and the laptop (RTX 4050); record versions, model digests and licences; a health endpoint.

## Why
Blocked AI features are blocked only by this; installing is a task, not a reason to drop features.

## How to implement
1. Install notes per machine and a check script (`tools/ai/check_hosts.py`).
2. Record digests in the licence register.
3. Health check endpoint in the inference service.

## Verification
Both machines answer a schema-constrained prompt and transcribe a test clip; versions recorded.

## Files changed
`tools/ai/check_hosts.py`, `docs/research/licence_provenance_register.md`

## Dependencies
Owner approval to install

## Common issues
Driver or CUDA mismatch on the GTX 1660 SUPER: use the runtime's bundled CUDA build.

## Sources
Features E57, E69, C33, C34.
