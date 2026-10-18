# p1-case-library-extended-s03 — Engine additions the P1 cases need (EXP-CASE-3)

Milestone: **M11 P1 content — theatre extension, extended cases, composer, Pulse**

## What
Only what the new cases require: maternity early-warning tool (placeholder per institutional choice), minor/guardian consent per law (Type C), capacitous refusal path + antibiotics-only option, interpreter workflow, anaphylaxis history handling (avoid cefazolin question per institutional decision 2), new-AF handling.

## Why
Some P1 cases need behaviour P0 never needed (maternity early-warning, guardian consent, capacitous refusal); adding exactly that — tested and without touching P0 behaviour — keeps the engine general.

## How to implement
1. EXP-CASE-3: list each case's unmet needs against the current schema/engine.
2. Implement each with tests; no case-specific code paths.
3. Each addition gets an ADR if it changes the engine's behaviour for P0 cases (it shouldn't).
4. Re-run the full P0 golden suite after each addition.

## Status (17 October 2026)

- **Result of EXP-CASE-3:** the generic correct-path segments of `tools/golden/p0_scripts.py` play all 16 P1 cases (10 variants and 6 stacked) to their expected end state with nothing refused, so **none needs an engine addition**: the maternity tool is the NEWS2 'not used' note, the minor and guardian consent, the capable refusal, the interpreter and the anaphylaxis history are in the consent and decision content, new atrial fibrillation is an ECG finding. STK-S2-C needs the emergency consent route. Each case got a correct-path golden (`tests/golden`; with the wrong paths of s04 there are 142 goldens in all, engine 0.0.9).
- **Limit:** the headset playthroughs (s04) are Sannan's.

## Verification
P0 golden suite still green after the additions; every P1 case expressible.

## Files changed
Core changes listed per need in `docs/plans/p1/p1-case-library-extended/README.md` (EXP-CASE-3 table), content flags in `content/cases/*`, tests in `tests/ProcSense.Core.Tests/`, ADRs in `docs/decisions/`.

## Dependencies
p1-case-library-extended-s02.

## Common issues
Consent law is institutional — content, not code.

## Sources
Case library README; walkthrough Part 10, Part 8.
