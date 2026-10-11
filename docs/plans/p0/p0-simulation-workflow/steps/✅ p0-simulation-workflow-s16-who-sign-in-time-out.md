# p0-simulation-workflow-s16 — Steps 13–14: WHO Sign In and Time Out engine

Milestone: **M3 Walking skeleton 3 — OT Sign In / Time Out**

## What
Checklist runtime with the exact WHO 2009 items (+ separately flagged ProcSense extensions): phase state NOT_STARTED → IN_PROGRESS → COMPLETE/STOPPED; each item's expected answer computed from state at answer time; NPC-owned items answered from state; contradiction → critical + stop until corrected; skipped item → challenge, repeated skip → case stops; Time Out also needs OR readiness; completion → READY FOR INCISION.

## Why
The P0 finish line (walkthrough Steps 13–14, Part 15).

## How to implement
1. `Checklist/SafetyChecklist.cs`: items from `content/who_checklist.csv` (`phase, item_id, prompt, expected_from_state, critical, is_extension, order`).
2. Verb `checklist_item(phase, item, response)`; `ExpectedFrom(item)` evaluates the rule → canonical value; mismatch → CRITICAL + STOPPED (T-SC2), resumes after correction (T-SC6).
3. Pulse-oximeter item can only be 'yes' if the sensor is attached (T-SC3, E13-B).
4. NPC-owned items (e.g. anaesthesia machine check) auto-answered from state after a short delay (T-SC5); P1 staff AI voices them, P0 uses pre-rendered lines (T-SC7).
5. S2 aspiration risk 'yes' → plan (RSI), not stop (T-SC8).
6. Time Out: antibiotic item checked against the treatments log time (E14-A); `complete(TIME_OUT)` requires OR_READY (T14-2) → end state READY_SAFE.
7. Hidden facts never asked surface here via the anaesthetist (e.g. VAR-S1-D(b) apixaban) → case stops (T02-WT1).

## Verification
`dotnet test --filter FullyQualifiedName~SafetyChecklist` → T-SC1…T-SC9, T13-1/2, T14-1/2 green; wording test compares every item to `who_checklist.csv` (T-SC9).

## Files changed
`src/ProcSense.Core/Checklist/SafetyChecklist.cs`, tests.

## Dependencies
p0-simulation-workflow-s14; p0-medical-research-data-s08 (checklist content).

## Common issues
Phase stuck STOPPED → the UI never emitted the correction action. Board order comes from the `order` field, not file order.

## Sources
Implementation Guide B-R15 Steps 1–4, 6 (T-SC1…T-SC9), §19–§20 (L13/L14, E13/E14, T13/T14); walkthrough Part 15, Steps 13–14, Part 12 Scene 11.
