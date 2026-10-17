# p1-live-scenario-generation-s02 — Retrieval index and context retrieval (OFF-05, LG-03)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
Tag filter plus BM25 index over approved content with SR ids; retrieval API for objectives.

## Why
The composer must only draw on approved evidence.

## How to implement
1. `tools/generation/build_index.py`; `server/generation/retrieve.py`.
2. Retrieval evaluation set; embeddings only if recall@20 below 0.9.

## Status (17 October 2026)

- **Built:** `server/generation/retrieve.py` (an in-memory index over the 53 case files, the 256 composer blocks and the 836 knowledge-base relations; tag filter, then BM25; documents below the allowed review status never returned; the index remembers the hash of the files it was made from and says when it is stale) and `tools/generation/build_index.py` (`--check` in CI, `--ask`). The evaluation set is `tests/generation/retrieval_queries.json` (246 queries: a problem label, the first sentence of why it matters, what the debrief says to remember; only approved cases are expected).
- **Verification result:** recall at 20 is 1.0 on the 246 queries (gate 0.9), the status filter holds (a DRAFT case and block exist and are held back), the index notices a changed file. Embeddings are not needed.
- **Limits:** the queries are made from the cases themselves, so recall measures whether retrieval finds what is there, not whether it understands a paraphrase; a human-written paraphrase set needs the labelling programme (p1-ai-infrastructure-s04). The index is built when needed (0.2 s) and is not a committed artefact, so there is no file to go stale; a service that keeps one in memory asks `is_current`.

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
