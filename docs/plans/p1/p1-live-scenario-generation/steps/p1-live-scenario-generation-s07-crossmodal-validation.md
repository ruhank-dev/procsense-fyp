# p1-live-scenario-generation-s07 — Cross-modal consistency and Stage B on generated packages (LG-09, LG-10)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Cross-modal rule table (VR-CM-*), Stage B and a headless correct-path run on each generated package.

## Why
No inconsistent or unsafe case reaches a trainee.

## How to implement
1. `content/generation/crossmodal_rules.json` with one pass and one fail fixture per rule.
2. Extend `tools/composer/mutate.py` with cross-modal breaks.
3. Headless run via the CLI.

## Verification
Every mutation caught; correct path reaches the declared end state.

## Files changed
`content/generation/crossmodal_rules.json`, `tools/composer/`

## Dependencies
p1-live-scenario-generation-s05, p1-live-scenario-generation-s06

## Common issues
Rule table drift: rules are content with `--check`.

## Sources
Spec LG-09, LG-10; A10, B18.
