# p1-ai-infrastructure-s02 — Benchmark on both GPUs with and without Unity running

Milestone: **M16 P1 AI infrastructure and personalisation — inference hosts, dual-GPU benchmark, labelling, learner model**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

> **Status (16 October 2026).** `tools/ai/benchmark.py` exists (tests BT0 speed and GPU share, BT1 structured output, BT2 grounding, and a blind rating sheet for BT3) with 13 tests, and the desktop alone was measured idle. Not done: the laptop, desktop and laptop with Unity and speech recognition loaded, thinking-on runs, BT4 debrief, BT5 composition, BT6 judge, the report `docs/research/model_benchmarks.md` (DT-188, DT-187). This step stays open.

## What
Measure for every candidate model on each GPU: VRAM, load time, time to first token, tokens per second, schema-valid rate on ProcSense schemas, ASR real-time factor and WER on a vocabulary set, and the Unity VR scene frame time while the model runs. Laptop RAM, CPU and power limits are recorded.

## Why
No model is chosen or rejected without numbers from our machines.

## How to implement
1. Benchmark protocol and prompts (`tools/ai/benchmark.py`).
2. Runs: desktop alone, laptop alone, laptop with Unity, desktop serving laptop over LAN.
3. Report `docs/research/model_benchmarks.md` with a default per task and machine.

## Verification
Report complete for every MC entry; repeat runs within 10 percent.

## Files changed
`tools/ai/benchmark.py`, `docs/research/model_benchmarks.md`

## Dependencies
p1-ai-infrastructure-s01

## Common issues
Thermal throttling on the laptop: run plugged in and record the power mode.

## Sources
Features E57, E69; OQ-01.
