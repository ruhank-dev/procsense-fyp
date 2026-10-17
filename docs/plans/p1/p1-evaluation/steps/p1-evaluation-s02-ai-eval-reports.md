# p1-evaluation-s02 — AI evaluation reports for the FYP

Milestone: **M12 P1 evaluation**

## What
Turn the weekly AI evaluation runs into report-ready evidence: tables and trend charts for grounding/disclosure leaks, schema validity, dialogue latency, classifier F1 per act, debrief mutation rejection, ASR WER and order accuracy, and fallback rate from the audit log — all generated from committed results, never typed.

## Why
P1's claims in the FYP report (AI is safe, grounded, fast enough) must rest on measured, reproducible data.

## How to implement
1. `tools/eval/collect_ai.py` → `docs/report/ai-eval.md`.
2. Tables: grounding/disclosure leaks per week, schema validity, dialogue latency p50/p95, classifier macro-F1 per act, debrief mutation rejection rate, ASR WER and order accuracy, fallback rate from the audit log.
3. Trend charts generated from committed weekly reports only (real data).
4. Short interpretation per metric with limitations.

## Status (17 October 2026)

- **Built:** `tools/eval/collect_ai.py` (`task report-ai`) writes `docs/report/ai-eval.md` from the result files in `reports/ai/`: narrative, planner, interpreter, critic, gateway under load, personalisation simulation, novelty simulation; a missing result reads "not run"; `--check` runs in CI so a result cannot change without the page changing.
- **Not built:** weekly trend charts (no weekly runs exist yet), classifier F1, debrief mutation rejection, WER (those suites do not exist).

## Verification
`task report-ai` regenerates `docs/report/ai-eval.md` from `reports/` with no hand-entered numbers.

## Files changed
`tools/eval/collect_ai.py`, `docs/report/ai-eval.md`.

## Dependencies
p1-ai-language-s09.

## Common issues
Reports from different model/prompt versions mixed together mislead — group by version from the audit log.

## Sources
Implementation Guide B-M5 (eval suite), B-M10 (audit report); INSTRUCTIONS §4c (real data only).
