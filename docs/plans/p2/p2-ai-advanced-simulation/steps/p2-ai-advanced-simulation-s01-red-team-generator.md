# p2-ai-advanced-simulation-s01 — Adversarial generator and red-team suite (E50)

Milestone: **M15 P2 learning analytics and advanced simulation**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Generate attacks on validators and dialogue: disclosure probes, jailbreaks, broken scenarios; report escape rates per release.

## Why
Gates are only as good as the attacks they have seen.

## How to implement
1. Extend mutate.py and make_vectors.py; LLM paraphrase of attacks.

## Verification
Escape rate reported; zero for critical classes.

## Files changed
`tools/redteam/`

## Dependencies
p1-ai-language-s09

## Common issues
Attacks leaking into training sets: keep sets separate.

## Sources
E50, A7.
