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

- **Labelling:** the relation labels the precision gate needs (100 relations, two labellers) can be made with `tools/labelling/label.py --kind relation`; the labellers are still the open need (DT-114, DT-210).

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
