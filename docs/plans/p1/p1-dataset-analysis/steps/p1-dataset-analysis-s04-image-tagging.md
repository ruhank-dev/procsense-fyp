# p1-dataset-analysis-s04 — Image library tagging and quality grading

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Give every picture in the imaging library (204) machine-readable finding tags, view, quality grade, real or synthetic flag, dataset id and licence; list findings the library cannot show.

## Why
Live generation picks images by tags; untagged images cannot be matched safely.

## How to implement
1. Finding tag vocabulary `content/imaging/finding_tags.json` aligned with case findings.
2. Extend `content/imaging_library.csv` columns (tags, quality) and its builder's `--check`.
3. Gap list: findings needed by blocks with no real or synthetic image (E64 stays rejected; gaps get written-only results or labelled synthesis).

## Verification
Every picture has tags and a quality grade; gap list written.

## Files changed
`content/imaging_library.csv`, `content/imaging/finding_tags.json`, `tools/content/imaging_library.py`

## Dependencies
p1-dataset-analysis-s01

## Common issues
Tags drifting from case vocabulary: validate against one vocabulary file.

## Sources
DS-0005, DS-0006, DS-0011, DS-0014; ADR-0003, ADR-0004.
