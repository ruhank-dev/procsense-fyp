# p1-evaluation

**Tier:** P1 (alongside P1 phases) · **Feeds:** P2, FYP report

## Goal
Evaluate everything P1 adds: AI eval suite results, voice accuracy, P1 golden runs, and a learning-effect study.

## Scope
- **In:** AI eval suite (B-M5) reports, synthetic test datasets with cards, P1 case golden runs, learning-effect study (pre/post), updated usability results.

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Learning-effect design | Pre/post knowledge + performance on repeated cases | Controlled trial vs non-VR group | Feasible with available participants | More participants available → controlled comparison |

## Experiments
| ID | Question | Method | Success metric | Status |
|---|---|---|---|---|
| EXP-EVAL-3 | Does practice improve outcomes? | Same users, repeated cases | Change reported without over-claiming | planned |
| EXP-EVAL-4 | Does AI dialogue improve perceived realism without hurting safety? | A/B: menu vs AI dialogue | Realism up, 0 safety violations | planned |

## Planning update (12 Oct 2026)
The evaluation of live scenario generation (band, profile, cross-modal, reproducibility, novelty, quality-judge calibration, latency) and of every AI feature is specified in `docs/architecture/testing_evaluation_plan.md`. s01 also produces the evaluation sets of `p1-dataset-analysis-s05`; s02 reports include the offline feedback loop LG-21; s03 can compare generated against library practice. Planning only.

## Exit
All P1 evaluation results recorded.

## Sources
Master Plan §41; Implementation Guide B-M5.
