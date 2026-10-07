# p0-clinical-logic

**Tier:** P0 · **Builds on:** p0-core-engine, p0-medical-research-data · **Feeds:** physiology, scoring, case-library, scenarios

## Goal
The medical rules, all driven by verified content: rule evaluator with provenance, NEWS2 + NG253 sepsis tiers and timers,
clinical calculators, investigations with realistic lab behaviour, medicine request→confirm→administer workflow, and the
decision & priority engine.

## Scope
- **In:** rule engine (every hit → register ID); NEWS2 Scale 1/2; sepsis tiers + timers + lateness; calculators (AIR/AAS/Alvarado,
  TG18, ASGE, ASA, STOP-BANG, SORT); investigation ordering, turnaround, sample-quality artefacts (haemolysed, drip arm, EDTA…),
  critical-result phone call + acknowledgement blocker; each completed investigation raises a `ResultReady` event that feeds the EMR and creates the printed report (p0-patient-records-emr, p0-diagnostic-transitions); medicine workflow + three checks + nurse challenge;
  DecisionOutcome {SAFE, INVESTIGATE, OPTIMISE, REVIEW, EMERGENCY}; problem types, priority ladder, clash checker.
- **Out:** values over time (physiology), score weights (scoring-debrief).

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Rule format | JsonLogic subset in content | Rules in C#; Lua scripting | Reviewable data; Python twin can validate content in CI | Too limited → typed C# rules with data parameters |
| Calculators | C# formulas, bands/thresholds from content | Pure data | Formulas are code, numbers are data | — |
| Doses | Never computed; educational examples display-only; patient-specific shown as "clinician decides" | Show doses | Walkthrough dose policy | — |
| Medicine roles | Trainee requests → clinician NPC confirms → nurse NPC administers | Trainee administers | Walkthrough Part 8.1 | — |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-CL-1 | Every walkthrough worked example reproduced? | Example-based tests | 100% (mismatch → issues.md, not a code tweak) |
| EXP-CL-2 | JsonLogic subset expressive enough? | Encode all P0 rules | ≥ 95% without custom operators |
| EXP-CL-3 | Is a Python rule-evaluator twin worth it? | Count content errors it catches | Keep only if it catches real errors |

## Exit
All worked examples pass; every rule hit has provenance; trainee can never dose/administer (tested).

## Who does what
Claude: all.

## Sources
Walkthrough Parts 4–10, 14B, 17; Implementation Guide B-M12, B-M13, B-M14, B-M20, B-R17, B-R18.
