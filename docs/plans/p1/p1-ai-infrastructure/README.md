# p1-ai-infrastructure

**Tier:** P1 · **Builds on:** p1-backend (gateway contract) · **Feeds:** p1-ai-language, p1-voice-input, p1-live-scenario-generation, p1-personalisation

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## Goal
Make every AI component runnable and measurable on our hardware: inference hosts, a benchmark on both GPUs with Unity running, a deployment topology with fallbacks, a labelling programme for evaluation and training sets, and the Unity-side hooks the AI features need.

## Scope
- **In:** installation, benchmarking, topology, labelling protocol and tools, Unity hook specifications.
- **Out:** feature logic (lives in the feature modules).

## Steps

| Step | Title |
|---|---|
| [p1-ai-infrastructure-s01](steps/p1-ai-infrastructure-s01-inference-hosts.md) | Inference hosts: install and pin the runtimes on both machines |
| [p1-ai-infrastructure-s02](steps/p1-ai-infrastructure-s02-dual-gpu-benchmark.md) | Benchmark on both GPUs with and without Unity running |
| [p1-ai-infrastructure-s03](steps/✅%20p1-ai-infrastructure-s03-deployment-topology.md) | Deployment topology, remote option and caching |
| [p1-ai-infrastructure-s04](steps/p1-ai-infrastructure-s04-labelling-programme.md) | Labelling programme for evaluation and training sets |
| [p1-ai-infrastructure-s05](steps/✅%20p1-ai-infrastructure-s05-unity-ai-hooks.md) | Unity-side AI hooks (specification for when Unity work is approved) |

## Exit
Every step's verification passes and `uv run --project tools tools/plans/validate_pipeline.py` still passes with the stage statuses updated.

## Sources
Planning update of 12 October 2026: `docs/architecture/` (feature registry, live generation spec, master roadmap), ADR-0008.
