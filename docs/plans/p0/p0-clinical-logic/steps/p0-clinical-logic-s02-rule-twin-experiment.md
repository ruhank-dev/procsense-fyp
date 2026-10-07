# p0-clinical-logic-s02 — Experiment EXP-CL-3: Python twin of the rule evaluator

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
B-M12 specifies a Python twin of the evaluator for validators and the composer. Run it as an experiment: build the minimal twin, run the same vectors through both in CI (T-JL1), and keep it only if it catches real content errors the schema checks miss.

## Why
A second implementation doubles maintenance (Ponytail: prove it's needed); the composer (P1) will need rule evaluation in Python, so the result matters.

## How to implement
1. `tools/content/jsonlogic.py` — same operators, no `eval()`.
2. CI step: run `content/rules/vectors.json` through C# (test) and Python; diff results (T-JL1).
3. Use it in `validate.py` to evaluate each rule's test cases at content-validation time.
4. After 2 weeks of content work, record: errors caught that the schema didn't → keep; none → delete and note the composer will call the C# CLI instead.

## Verification
Decision recorded in the clinical-logic README with evidence (count of errors caught).

## Files changed
`tools/content/jsonlogic.py` (kept or deleted), CI workflow, README experiment log.

## Dependencies
p0-clinical-logic-s01.

## Common issues
Vectors are the contract — fix semantics in both or neither.

## Sources
B-M12 Step 2, T-JL1; clinical-logic README EXP-CL-3.
