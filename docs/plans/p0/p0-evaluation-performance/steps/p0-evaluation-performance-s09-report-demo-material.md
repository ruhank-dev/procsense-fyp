# p0-evaluation-performance-s09 — FYP report and demo material

Milestone: **M9 P0 release**

## What
Collect results already recorded in phase READMEs and experiment logs into the FYP report draft (method, architecture, medical proof-of-truth process, experiments, evaluation results); record a demo video.

## Why
The results are already written down — reuse them.

## How to implement
1. `tools/eval/collect_experiments.py` → experiment tables.
2. Report draft in `docs/report/`; demo video script.
3. `tools/eval/collect_experiments.py` scans every phase README experiment table and results folder → `docs/report/experiments.md` (question, method, metric, result, decision).
4. Report outline in `docs/report/README.md`: problem & aims, walkthrough-driven design, architecture (Core / Unity / AI boundary), proof-of-truth process (register, issues, review packs), implementation per phase, evaluation (golden suite, usability, clinical realism, performance), limitations, future work (P1/P2).
5. Figures: architecture diagram (from Master Plan §25), sample debrief, performance charts, usability tables — all from real data.
6. Demo video script: VAR-S1-A happy path (3 min) + VAR-S2-PD deterioration with debrief lateness (3 min).

## Verification
`task report-experiments` regenerates the experiments chapter; you review the draft outline and the demo video.

## Files changed
`docs/report/*`, `tools/eval/collect_experiments.py`.

## Dependencies
p0-evaluation-performance-s08.

## Common issues
Numbers in the report must come from recorded results — never typed in (INSTRUCTIONS §4c real data only).

## Sources
Phase READMEs (experiment logs); Master Plan (architecture §25–27); INSTRUCTIONS §4c.
