# p1-dataset-analysis-s06 — Scenario knowledge base: relations from tables, cases, walkthrough and datasets (A66)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
Build a typed graph of condition-finding, test-finding, decision-consequence and interaction relations: imported from the verified tables, extracted by an LLM from case files and walkthrough text (offline, quote-checked, reviewed), and quantified from dataset profiles with sample sizes and confidence intervals.

## Why
The AI composer must learn relationships from evidence instead of drawing independent random values; the graph is what it may use.

## How to implement
1. Relation schema `content/kb/schema.json`; importer for bands, critical results, findings, decision rules, what-ifs with SR ids.
2. Extraction CLI: chunk case files and walkthrough sections, LLM extraction with a quote that must appear verbatim, review queue.
3. Strengths from p1-dataset-analysis-s02 to s04 profiles with n and bootstrap intervals; n under 50 marked weak; an outlier is never recorded as an abnormality without a band or source.
4. Compatibility suggestions for blocks (A12.c2) from the graph.

## Status (17 October 2026)

- **Built:** `content/kb/schema.json` and `tools/kb/build_kb.py` (`--check` in CI, `--neighbours`): 836 relations imported from the bands, critical limits, test indications, treatment clashes, what-ifs, the authored cases (problem to abnormal result and to expected action), the image tags and the dataset correlations; each has an SR, DS or CASE source, a confidence (verified, authored, statistical) and, for statistical ones, n and a 95 % interval with weak marked under 50. `tools/kb/extract_relations.py` asks the offline model (qwen3.5:9b) for candidate relations from case and walkthrough chunks, keeps one only if its quote appears word for word and both ends are in the quote, queues it unreviewed (`content/kb/review_queue.json`), and imports nothing until a person approves it (`--approve ID --by NAME`); `--sheet` and `--score` give a labeller the sample and the precision.
- **Measured:** a first run on 16 case chunks gave 33 proposed, 21 kept, 12 dropped as ungrounded (quote not word for word 1, subject not in the quote 7, object not in the quote 4).
- **Not met:** the gate of precision at least 0.8 on 100 hand-labelled relations and recall above a keyword baseline needs a person to label (DT-114, p1-ai-infrastructure-s04); until then no model relation is in the base. Compatibility suggestions for blocks (A12.c2) wait for the block library (p1-scenario-composer-difficulty-s01).

## Verification
Every relation has a source; extraction precision at least 0.8 on 100 hand-labelled relations and recall above a keyword baseline; importer `--check` passes.

## Files changed
`content/kb/`, `tools/kb/build_kb.py`, `tools/kb/extract_relations.py`

## Dependencies
p1-dataset-analysis-s02, p1-dataset-analysis-s03, p1-dataset-analysis-s04; p1-ai-infrastructure-s01 for the extraction model

## Common issues
Treating co-occurrence in 53 cases as causation: relations carry type and confidence; reviewers approve.

## Sources
Feature A66; spec LG-03, LG-04.
