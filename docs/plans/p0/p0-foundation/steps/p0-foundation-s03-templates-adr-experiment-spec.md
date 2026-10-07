# p0-foundation-s03 — Templates: ADR, experiment record, equipment spec sheet

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
Three short markdown templates: an ADR (context, decision, alternatives, consequences, revisit-if), an experiment record (question, method, metric, result, decision changed?, date), and an equipment reference sheet (dimensions + source actually opened, components, materials/colours, states, interaction points, triangle-budget class).

## Why
The plan changes through experiments; decisions and results written in the same shape are trustworthy later, easy to compare, and drop straight into the FYP report. Equipment reference sheets are the proof of truth behind every Blender asset.

## How to implement
1. `docs/decisions/_template.md`; bring ADR-0001 in line with it.
2. `docs/plans/_experiment-template.md`; experiment results go into each phase README's experiment table using this shape.
3. `docs/research/equipment/_template.md` with a worked example filled for the patient wristband.
4. Experiment template includes 'status: planned | running | done' so phase READMEs show which experiments are open.

## Verification
ADR-0001 follows the template; one equipment sheet exists as the worked example.

## Files changed
`docs/decisions/_template.md`, `docs/plans/_experiment-template.md`, `docs/research/equipment/_template.md`, `docs/research/equipment/wristband.md`.

## Dependencies
p0-foundation-s00.

## Common issues
Templates are checklists — keep each under one screen.

## Sources
INSTRUCTIONS §8b; plans README 'How the plan changes as we learn'.
