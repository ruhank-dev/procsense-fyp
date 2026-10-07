# p1-evaluation-s03 — Learning-effect study (EXP-EVAL-3)

Milestone: **M12 P1 evaluation**

## What
Measure whether practising with ProcSense improves performance: the same participants play matched cases in two sessions (≥ 1 day apart) with a short knowledge quiz before and after; compare quiz scores, critical items, correct end states and time to key decisions.

## Why
The FYP benefits from evidence of learning, not just usability — reported honestly with its limits.

## How to implement
1. Protocol + consent; sessions; analysis script.
2. Design: same participants play a matched case pair twice (session 1 and session 2, ≥ 1 day apart); short knowledge quiz before/after (questions from walkthrough teaching points).
3. Measures: quiz score change, critical items count, end-state correctness, time to key decisions.
4. Analysis: paired comparison with effect sizes; no causal over-claiming (no control group in P1).

## Verification
Results table + limitations section written from raw data.

## Files changed
`docs/plans/p1/p1-evaluation/results/learning-effect.md`, quiz `content/eval/quiz.json`, analysis script `tools/eval/analyse_learning.py`.

## Dependencies
M9.

## Common issues
Learning the specific case (not the skill) inflates gains — use matched but different cases in session 2.

## Sources
p1-evaluation README EXP-EVAL-3; Master Plan §41.
