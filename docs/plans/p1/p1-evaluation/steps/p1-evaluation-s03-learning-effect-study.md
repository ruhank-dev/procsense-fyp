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

## Status (17 October 2026)

- **Built (the part that needs no participant):** a draft protocol with the consent text (`docs/plans/p1/p1-evaluation/protocols/learning-effect.md`), the analysis (`tools/eval/analyse_study.py learning`: paired differences, Hedges' g, bootstrap intervals, exact sign-flip tests, the limitations paragraph; tests on simulated data in `tools/ci/test_study_analysis.py`, which cannot be written into the results folder) and the list of 46 confirmed teaching points as quiz candidates (`tools/eval/build_quiz.py`, `content/eval/quiz_candidates.json`).
- **Blocked on people:** the supervisor's agreement, a clinician to turn candidates into questions with wrong answers, participants (target 12). No result exists; `results/learning-effect.md` is not written (DT-206).

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
