# p1-live-scenario-generation-s12 — Grounded debrief, history and the offline feedback loop (LG-19 to LG-21)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Debrief cites the generated package's facts; history stores fingerprint and package hash; the offline evaluation report proposes pack, prompt and threshold changes for a reviewed release.

## Why
Closes the loop without ever retraining live.

## How to implement
1. Extend DebriefFacts with package provenance.
2. `tools/generation/evaluate.py`.

## Verification
Every change to packs or prompts cites an evaluation report.

## Files changed
`tools/generation/evaluate.py`

## Dependencies
p1-live-scenario-generation-s11, p1-ai-language-s08

## Common issues
Feedback loop must not run inside sessions.

## Sources
Spec LG-19 to LG-21; E51.
