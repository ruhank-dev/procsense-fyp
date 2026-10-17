# p1-dataset-analysis-s04 — Image library tagging and quality grading

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
Give every picture in the imaging library (204) machine-readable finding tags, view, quality grade, real or synthetic flag, dataset id and licence; list findings the library cannot show.

## Why
Live generation picks images by tags; untagged images cannot be matched safely.

## How to implement
1. Finding tag vocabulary `content/imaging/finding_tags.json` aligned with case findings.
2. Extend `content/imaging_library.csv` columns (tags, quality) and its builder's `--check`.
3. Gap list: findings needed by blocks with no real or synthetic image (E64 is a deferred offline research track, not used at runtime; gaps get written-only results or labelled synthesis).

## Status (17 October 2026)

- **Built:** `tools/content/imaging_tags.py` (`--check` in CI) writes `content/imaging/finding_tags.json` (a closed vocabulary of tags; 60 findings tagged, each tag supported by the finding's own wording or its test, checked), `content/imaging/picture_tags.csv` (all 204 pictures: finding, set, modality, view where stated, tags, technical quality grade, size, exposure, contrast, sharpness, real or synthetic, dataset, licence, approval status) and `docs/research/imaging_gaps.md`.
- **Verification result:** every picture and finding has tags and a grade (A 102, B 102, C 0); the gap list is written: 29 findings have synthetic pictures only, 7 ultrasound findings have no wording in the book, 3 tests return written results only.
- **Differences from the plan:** the per-picture table is a new file (`picture_tags.csv`) and `imaging_library.csv` (one row per finding) is unchanged. The quality grade is technical (size, exposure, contrast, sharpness against the median of its own set), not diagnostic; the view is only given where the dataset or the generator states it (ECG lead II, chest films frontal, CT and MRI axial, MRCP coronal); ultrasound views are not stated.

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
