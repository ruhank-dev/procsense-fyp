# p1-ai-language-s01 — Grounding / validation layer

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
Validator run on every AI output before it reaches the trainee: (1) JSON schema + enum membership; (2) grounding — fact IDs ⊆ context facts, no lexicon entity (drug, class, allergen, condition, test, finding, equipment) outside the case's allowed set, every number present in context; (3) disclosure — no undisclosed fact value unless its `disclose_on` is satisfied this turn; (4) intent ∈ approved intents; (5) debrief — fact IDs exist, verdicts/numbers unchanged, no events not in the log; dose-policy check — reject number+unit for medicines unless the exact EDUCATIONAL EXAMPLE text is in context, reject 'give/prescribe' phrasing to the trainee. Fail → one regeneration with reasons → still fail → template/pre-rendered line; always audited.

## Why
Built first, so no model output can ever reach a trainee unchecked.

## How to implement
1. Lexicons generated at content-package time (lemmatised token sets) + stop-list for common words.
2. Python implementation on the server; C# twin for cached lines on device; shared vectors (T-GV5).
3. Adversarial suites: 100 disclosure probes, off-card drug names, mutated debrief numbers, dose statements.

## Verification
T-GV1 0 leaks, T-GV2 0 accepted off-card drugs, T-GV3 ≤ 2% false rejects, T-GV4, T-GV5, T-GV6 green.

## Files changed
`server/validation/*`, `src/ProcSense.Core/AIContext/Validator.cs` (twin), vectors, tests.

## Dependencies
p1-backend-s01; p0-medical-research-data (lexicon sources).

## Common issues
Synonyms slip through → extend lexicon with synonyms/brand names.

## Sources
Implementation Guide B-R2 Steps 1–9, T-GV1–T-GV6.
