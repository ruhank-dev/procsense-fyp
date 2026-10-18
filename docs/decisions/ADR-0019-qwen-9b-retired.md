# ADR-0019 — The Qwen 3.5 9B model is retired; the 4B runs every workload

**Date:** 19 October 2026 (fifth session) · **Status:** Accepted (owner's decision, recorded in the session prompt of that day; engineer implementation) · **Related:** ADR-0010, ADR-0017, ADR-0018

## Context
ADR-0017 moved every production prompt to the 4B and kept the 9B for three offline uses: relation extraction, the cross-check labeller (`tools/bench/cross_judge.py`) and as the judge of the extraction comparison. The owner decided not to maintain a second model for these.

## Decision
1. `qwen3.5:4b-q4_K_M` is the only language model of the project. Active tools default to it: `tools/kb/extract_relations.py`, `tools/kb/judge_relations.py`, `tools/bench/cross_judge.py` (new third labeller J4 is a 4B configuration), `tools/ai/model_resources.py`.
2. Historical 9B results stay as records and are labelled historical: `reports/ai/*-9b.json`, `reports/ai/kb-extract-4b-vs-9b.json`, `docs/research/model_4b_vs_9b.md`, the J3 label files of the synthetic classifier sets, `content/kb/review_queue.json` (proposals the 9B made, awaiting a person's approval, with their provenance). They are not regenerated and nothing reads them to decide a runtime behaviour.
3. The local Ollama model `qwen3.5:9b-q4_K_M` was removed from the desktop.
4. Where the 9B judge was used (relation support), the judgement is now the 4B in two configurations plus deterministic checks; it is an AI-judged proxy, never independent. See `docs/research/kb_relation_metric_audit.md` for what that metric measures and what it does not.

## Consequences
One model to install and load. Judge and extractor are the same model, so the proxy can favour its own output; independent relation precision stays EX-10. Nothing safety-relevant depended on the 9B.

## Revisit if
A task appears where the 4B fails its gate and no deterministic or authored fallback exists.
