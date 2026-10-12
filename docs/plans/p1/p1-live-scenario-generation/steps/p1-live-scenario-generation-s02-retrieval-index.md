# p1-live-scenario-generation-s02 — Retrieval index and context retrieval (OFF-05, LG-03)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Tag filter plus BM25 index over approved content with SR ids; retrieval API for objectives.

## Why
The composer must only draw on approved evidence.

## How to implement
1. `tools/generation/build_index.py`; `server/generation/retrieve.py`.
2. Retrieval evaluation set; embeddings only if recall@20 below 0.9.

## Verification
Recall@20 at least 0.9; status filter test.

## Files changed
`tools/generation/build_index.py`, `server/generation/retrieve.py`

## Dependencies
p1-scenario-composer-difficulty-s01

## Common issues
Stale index after content change: index hash tied to content manifest.

## Sources
Spec LG-03; E58.
