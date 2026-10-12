# p0-scoring-debrief-s05 — Session report export

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
`procsense-sim report --log <session.jsonl> --out report.html` (and JSON): end state, scores with evidence links, timeline, debrief text, sources for every clinical statement. Unity uses the same JSON for the debrief screen; the backend (P1) reuses the format.

## Why
Supervisor, medical reviewer and the FYP report need readable results from any session, including headset sessions.

## How to implement
1. CLI `report` command → JSON + a single self-contained HTML file (no web framework).
2. Taskfile: `task report -- --log <file>`.
3. Test: report generated for every golden run.
4. HTML is one file with inline CSS (no JS needed), readable on a phone; sections: summary, criticals, categories, timeline, sources.

## Verification
`task report -- --log tests/golden/VAR-S1-A_correct.log.jsonl` writes `report.html`; every clinical statement in it shows a source.

## Files changed
`src/ProcSense.Cli/ReportCommand.cs`, `Taskfile.yml`, tests.

## Dependencies
p0-scoring-debrief-s04.

## Common issues
HTML that pulls external CSS/JS breaks offline — inline everything. Report must not include real names (pseudonymous session IDs only).

## Sources
Master Plan §25 (evaluation/feedback, logging/analytics); Implementation Guide B-M15 Step 5, B-M16 Step 3.
