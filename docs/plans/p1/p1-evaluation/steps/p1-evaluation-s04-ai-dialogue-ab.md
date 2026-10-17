# p1-evaluation-s04 — AI dialogue A/B (EXP-EVAL-4)

Milestone: **M12 P1 evaluation**

## What
Controlled comparison of the two dialogue modes on the same case family: menu-only (P0) vs AI free-text dialogue (P1), counterbalanced order, measuring perceived realism, task success, time, validator rejections/fallbacks, and any safety issue in the audit log.

## Why
AI dialogue is the riskiest P1 feature; it should ship on by default only with evidence that it improves realism without any safety cost.

## How to implement
1. A/B protocol; audit review.
2. Counterbalanced: half start with menu-only, half with AI dialogue; same case family.
3. Measures: realism rating, task success, time, number of validator rejections/fallbacks, any safety issue found in the audit log.
4. Any safety violation → feature stays off by default.

## Status (17 October 2026)

- **Built (the part that needs no participant):** a draft protocol with the decision rule fixed beforehand (`docs/plans/p1/p1-evaluation/protocols/ai-dialogue-ab.md`: any safety issue keeps the feature off; otherwise realism must be higher with sign-flip p under 0.05) and the analysis (`tools/eval/analyse_study.py ab`: counterbalanced order effect, validator rejections and fallbacks, safety issues from the audit log, the decision text).
- **Blocked on people:** the supervisor's agreement and participants (DT-206). `results/ai-dialogue-ab.md` is not written.

## Verification
A/B results table; audit review notes; feature-flag decision recorded.

## Files changed
`docs/plans/p1/p1-evaluation/results/ai-dialogue-ab.md`, protocol, `ai_features.yaml` decision.

## Dependencies
p1-ai-language-s04.

## Common issues
Novelty effect inflates realism ratings for AI — counterbalancing and a second session reduce it.

## Sources
p1-evaluation README EXP-EVAL-4; Master Plan §26–27, §41 (T-AI-04, T-AI-06); Implementation Guide B-M10 (audit).
