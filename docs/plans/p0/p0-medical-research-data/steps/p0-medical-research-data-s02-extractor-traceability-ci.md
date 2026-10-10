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

## Implementation notes (10 Oct 2026)
- `tools/content/extract_walkthrough.py` reads the walkthrough and writes `content/_extract/walkthrough_extract.json` (git-ignored: it is a drafting aid you recreate with `task content-extract`, about 340 KB). It extracts 15 steps with their answers, 47 what-ifs, 24 VR cards, 8 full + 20 compact stacked cases, the two 20-stage flow maps (10 fields each) and the 29 parts of the book. CI runs it with `--check`.
- **One count in the Change Report does not match the book** (ISS-0003, open for the owner): the book answers 270 of the 315 "15 × 21" question slots, because each step leaves out the questions that do not apply. The what-if count of 47 matches (a first run found 48, which was the extractor reading a Part 9 heading; fixed, ISS-0004 closed). The check pins what the book really contains; a new walkthrough version will fail it on purpose.
- `tools/content/trace.py` generates `docs/research/traceability.md`. A part is mapped when a register row cites it and a content file uses that row; otherwise `tools/content/trace_status.json` must say it is planned in a real step (the step file must exist) or not used (with a reason). Today: 3 parts mapped (11, 12, E, each partly), 22 planned, 4 not used. CI fails on a stale file or an unaccounted part (T-WT1).
- `tools/content/checks.py`: any CSV table in `content/` needs a `ref` column and every row must cite register ids that exist; JSON content already needed refs.
- Tests: `tools/content/test_content_tools.py` (20 tests: counts, a flow stage against the book, a missing what-if or stage or answer, unaccounted/unplanned parts, CSV refs, a value without a source). CI runs them as "content tools tests".
- Taskfile: `content-extract`, `content-trace`.
