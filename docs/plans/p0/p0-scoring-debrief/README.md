# p0-scoring-debrief

**Tier:** P0 (AI-worded debrief in p1-ai-language) · **Builds on:** p0-simulation-workflow, p0-clinical-logic · **Feeds:** vr-interaction-ui (debrief screen), p1-ai-language, evaluation

> **Progress, 12 Oct 2026:** s01 to s05 are built and tested, awaiting approval: 22 assessed behaviours with a rubric, the evidence mapper with its critical guard, the outcome and fail gate, debrief facts with template sentences, and the `report` command. Weights and points are provisional (Q8).

## Goal
Fair, explainable judgement: score items with evidence references, five end states (appropriate postponement = success),
safety-failure detection, lateness, and a timeline debrief built only from logged facts.

## Scope
- **In:** score items + rubrics (with "acceptable alternatives"), evidence mapper, end-state resolution, DebriefFacts, template debrief, session report export.
- **Out:** AI wording, debrief visuals.

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Who scores | Deterministic rules only | AI judgement | Fair, reproducible | — |
| Speech/classifier evidence (P1) | Counts above threshold, never for critical items | Full credit | Safety | — |
| Debrief text | Templates from DebriefFacts | AI only | Offline, no invented mistakes | — |
| Weights | Initial weights from walkthrough priorities, tuned with playtest data | Fixed | Needs data | — |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-SC-1 | Golden runs (correct/novice/dangerous/edge) give expected results? | Golden suite | 100% |
| EXP-SC-2 | Do trainees understand the debrief? | Usability session | Can name top mistake + why |

## Exit
All P0 golden runs pass; every debrief sentence links to a log entry.

## Who does what
Developer: all.

## Sources
Walkthrough Part 16, Part D §16–19; Implementation Guide B-M15, B-M3.
