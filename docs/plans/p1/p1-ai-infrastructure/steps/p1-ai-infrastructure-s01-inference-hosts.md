# p1-ai-infrastructure-s01 — Inference hosts: install and pin the runtimes on both machines

Milestone: **M16 P1 AI infrastructure and personalisation — inference hosts, dual-GPU benchmark, labelling, learner model**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Install and pin Ollama with the Qwen3 1.7B, 4B and 8B tags, faster-whisper (small.en, medium.en) and a TTS engine on the desktop (GTX 1660 SUPER) and the laptop (RTX 4050); record versions, model digests and licences; a health endpoint.

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
