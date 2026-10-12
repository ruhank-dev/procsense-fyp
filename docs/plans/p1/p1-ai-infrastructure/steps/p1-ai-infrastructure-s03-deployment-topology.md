# p1-ai-infrastructure-s03 — Deployment topology, remote option and caching

Milestone: **M16 P1 AI infrastructure and personalisation — inference hosts, dual-GPU benchmark, labelling, learner model**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Decide from the benchmark: split mode (inference on the desktop, Unity on the laptop, LAN), single-machine mode (small models or CPU plus the pool), and an optional remote inference mode for offline batch jobs only, never with personal data; caching of generated packages and lines.

## Why
Hardware limits are handled by design, not by dropping features.

## How to implement
1. Topology document and config switches.
2. Fallback matrix per capability.
3. Network failure tests.

## Verification
Each AI capability has a deployment and a tested fallback.

## Files changed
`docs/architecture/deployment.md`, service config

## Dependencies
p1-ai-infrastructure-s02

## Common issues
LAN latency spikes: pool and timeouts.

## Sources
Feature E69.
