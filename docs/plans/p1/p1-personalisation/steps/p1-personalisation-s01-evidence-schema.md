# p1-personalisation-s01 — Session evidence record

Milestone: **M16 P1 AI infrastructure and personalisation — inference hosts, dual-GPU benchmark, labelling, learner model**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Define and extract SessionEvidence v1 at the end of every session: completed and missed steps with lateness, errors with severity, decision sequence, communication acts with source and confidence, investigation interpretation actions, responses to events with latency, hints and assistance used, confidence reports.

## Why
Personalisation is only as good as the evidence it learns from.

## How to implement
1. Schema `server/schemas/session_evidence.json`.
2. Extractor from the session log (C# or Python twin with shared vectors).
3. Pseudonym only; no free text from the trainee stored.

## Verification
Every golden session yields a valid record identical in both extractors.

## Files changed
`server/schemas/session_evidence.json`, extractor

## Dependencies
p0-scoring-debrief (built), p1-backend-s02

## Common issues
AI-sourced acts stored with confidence, never as certain.

## Sources
Features D67, D40.
