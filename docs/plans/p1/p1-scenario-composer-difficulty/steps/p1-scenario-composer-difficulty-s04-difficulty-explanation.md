# p1-scenario-composer-difficulty-s04 — Difficulty explanation

Milestone: **M11 P1 content — theatre extension, extended cases, composer, Pulse**

## What
When practice mode picks the next case, the trainee sees why: a template sentence built only from the selector's facts (chosen dimension, weakest competency, recent score), optionally reworded by the LLM through the validator.

## Why
Transparent adaptation builds trust and tells trainees what to focus on.

## How to implement
1. Template first; LLM variant behind the feature gate.
2. Template: 'Next: <case title>. Focus: <dimension>. Why: your recent <competency> score was <x>.' — facts from the selector only.
3. Optional LLM rewording via the validator (facts unchanged).

## Status (17 October 2026)

- **Built earlier (P0):** the template explanation is `Selection.Explanation` of `src/ProcSense.Core/Difficulty/Difficulty.cs` from `content/difficulty/dimensions.json`, built from the selector's facts only and tested in `DifficultyTests`; `server/analytics/next_case.py` adds the matched pattern to the reason when it re-orders the valid set.
- **Not built:** the optional language-model rewording behind the gate: the template is complete and a rewording could only risk changing a number.

## Verification
Explanation test: every number/dimension in the text matches the selector output.

## Files changed
`src/ProcSense.Core/Difficulty/Explanation.cs`, `content/difficulty/explanation_templates.json`, Unity next-case card, tests.

## Dependencies
p0-case-library-s06.

## Common issues
An LLM rewording that changes a number or dimension is rejected by the validator — template shown instead.

## Sources
Implementation Guide B-M7 Step 4; Master Plan §34 (adaptive difficulty).
