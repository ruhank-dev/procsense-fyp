# p1-personalisation-s01 — Session evidence record

Milestone: **M16 P1 AI infrastructure and personalisation — inference hosts, dual-GPU benchmark, labelling, learner model**


## What
Define and extract SessionEvidence v1 at the end of every session: completed and missed steps with lateness, errors with severity, decision sequence, communication acts with source and confidence, investigation interpretation actions, responses to events with latency, hints and assistance used, confidence reports.

## Why
Personalisation is only as good as the evidence it learns from.

## How to implement
1. Schema `server/schemas/session_evidence.json`.
2. Extractor from the session log (C# or Python twin with shared vectors).
3. Pseudonym only; no free text from the trainee stored.

## Status (17 October 2026)

- **Built:** `server/schemas/session_evidence.json` and `server/personalisation/evidence.py` (`extract`): steps completed and missed with lateness, errors by severity, actions in order, events, credits and penalties per behaviour, the story and tier of a generated case; a pseudonym and codes only. What the engine does not record (spoken acts, hints, confidence reports, event response latency, interpretation actions) is listed as `unrecorded`. Six golden sessions and a generated one give valid, deterministic records (`tools/ci/test_personalisation_evidence.py`).
- **Not built:** a C# twin of the extractor (the report is the shared source; one extractor).

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
