# p2-ai-advanced-simulation-s02 — Patient Digital Twin (E48)

Milestone: **M15 P2 learning analytics and advanced simulation**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Persistent patient state across a session and repeat sessions from the generated profile and physiology.

## Why
Continuity makes consequences real.

## How to implement
1. Twin state object over LitePhysio or PULSE; replayable.

## Verification
Twin state replays.

## Files changed
`src/ProcSense.Core/Twin/` (research)

## Dependencies
p1-live-scenario-generation-s11

## Common issues
State explosion: keep the twin to the case's systems.

## Sources
E48.
