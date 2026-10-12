# p1-live-scenario-generation-s04 — AI composition: narrative, profile wording, persona (LG-06)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
LLM fills narrative slots through the AI gateway with a JSON schema; grounding validator; template fallback.

## Why
Language is the product here; values are not.

## How to implement
1. Prompt `prompts/compose/v1.txt`; schema with enums.
2. One retry with defect avoid-hint; then template.
3. Persona tags vocabulary (DT-085).

## Verification
First-try schema validity at least 95 percent; zero grounding escapes.

## Files changed
`server/generation/compose.py`, `prompts/compose/`

## Dependencies
p1-ai-language-s02, p1-backend-s06, p1-live-scenario-generation-s03

## Common issues
Model drifts into values: values are hidden or slot tokens in the prompt.

## Sources
Spec LG-06; A3, C29.
