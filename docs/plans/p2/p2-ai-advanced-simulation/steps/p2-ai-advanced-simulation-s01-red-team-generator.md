# p2-ai-advanced-simulation-s01 — Adversarial generator and red-team suite (E50)

Milestone: **M15 P2 learning analytics and advanced simulation**


## What
Generate attacks on validators and dialogue: disclosure probes, jailbreaks, broken scenarios; report escape rates per release.

## Why
Gates are only as good as the attacks they have seen.

## How to implement
1. Extend mutate.py and make_vectors.py; LLM paraphrase of attacks.

## Status (17 October 2026)

- **Built:** `tools/redteam/run.py`: attacks on the grounding validator on the hidden facts of the authored cases in seven classes; report `docs/research/redteam_report.md` and `reports/ai/redteam.json`; a test keeps the report current and the critical classes at zero.
- **Result:** critical classes (verbatim, numbers) escapes: 0. Not critical but open: half-word disclosures 79.7 percent, synonyms 37.5 percent, made-up denials 100.0 percent (DT-211).

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
