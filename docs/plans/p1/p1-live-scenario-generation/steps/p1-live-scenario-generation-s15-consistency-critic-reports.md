# p1-live-scenario-generation-s15 — Generated reports and the cross-modal text consistency critic

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Write investigation reports from approved templates with numbers locked to the generated values (LLM paraphrase optional, numbers verified), and run an LLM critic that extracts claims from all generated text (narrative, reports, dialogue seeds) and compares them with the structured case; any unresolved conflict rejects the package.

## Why
Contradictions hidden in free text are the ones rules miss.

## How to implement
1. Report templating with number lock.
2. `server/generation/critic.py`: claim extraction prompt, comparison, defect output.
3. Seeded set of 200 text inconsistencies.

## Verification
Critic recall at least 20 points above rules plus lexical validator, false alarms under 10 percent.

## Files changed
`server/generation/reports.py`, `server/generation/critic.py`

## Dependencies
p1-live-scenario-generation-s07, p1-ai-infrastructure-s02

## Common issues
Critic false alarms block good cases: tune on the seeded set; unresolved flags go to repair, not silent pass.

## Sources
Features B18, B22.
