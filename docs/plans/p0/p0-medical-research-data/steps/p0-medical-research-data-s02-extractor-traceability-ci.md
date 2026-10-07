# p0-medical-research-data-s02 — Walkthrough extractor, traceability and CI

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
(a) `extract_walkthrough.py` parses the walkthrough markdown into `walkthrough_extract.json` (steps × 21 questions, what-ifs, VR cards, stacked cases, flow stages, tables) with count checks; (b) the validator is generalised so every clinical row in any `content/` file must carry a register ref; (c) `trace.py` generates `docs/research/traceability.md` mapping each walkthrough section → content files → register rows, failing CI for an unmapped section.

## Why
Drafting tables from the extract makes the following transcription steps verify-and-fix instead of retyping; traceability makes gaps visible.

## How to implement
1. `tools/content/extract_walkthrough.py` reading `docs/context/ProcSense_Medical_Walkthrough_v5_1.md` → `content/_extract/walkthrough_extract.json` (generated, not hand-edited).
2. Count test T-WT2: 15 steps × 21 questions, 47 what-ifs, 24 VR cards, 8 full + 20 compact stacked cases, 2 × 20 flow stages (counts from the Change Report).
3. Generalise `checks.py`: CSV rows need a `ref` column; JSON objects need `ref`/`refs`.
4. `tools/content/trace.py` → `docs/research/traceability.md` (header 'generated — do not edit'); T-WT1: every walkthrough Part is mapped or explicitly marked 'not used (reason)'.
5. Taskfile `content-extract`, `content-trace`; both in CI.

## Verification
`task content-extract` prints the expected counts; `task content-validate` + `task content-trace` green; removing a ref fails CI.

## Files changed
`tools/content/extract_walkthrough.py`, `tools/content/trace.py`, `tools/content/checks.py`, `content/_extract/`, `docs/research/traceability.md`, `Taskfile.yml`.

## Dependencies
p0-medical-research-data-s01; p0-foundation-s02.

## Common issues
Walkthrough tables were flattened in the source (Conversion Report §5.1) — the extractor drafts, a human/the developer check confirms; never trust drafted numbers blindly.

## Sources
Implementation Guide B-M18 Steps 1, 3–5, T-WT1/T-WT2; Conversion Report §5; Change Report §A.
