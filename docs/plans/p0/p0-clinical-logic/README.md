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
Developer: all.

## Sources
Walkthrough Parts 4–10, 14B, 17; Implementation Guide B-M12, B-M13, B-M14, B-M20, B-R17, B-R18.

## Experiment log

### EXP-CL-3: is the Python rule-evaluator twin worth it? (11 Oct 2026)
- **Built:** `tools/content/jsonlogic.py` (about 250 lines, no `eval()`), run against the same `content/rules/vectors.json` as the C# evaluator: 156 vectors and 14 error vectors agree in both (T-JL1, `tools/content/test_jsonlogic.py`, CI row 7ad). The first run agreed everywhere.
- **Used:** `validate.py` compiles every workflow rule with the twin and refuses a rule that reads a variable the engine does not provide (the list is read from `CaseGraphSubsystem.cs`, so it cannot go stale), an unknown operator, a wrong argument count or a computed variable name. The schema checks none of these.
- **Evidence so far:** 0 real content errors caught (all 20 workflow and fixture rules were already correct); 4 injected errors of the kinds above are all caught by the tests.
- **Decision:** keep, provisionally. It costs one file and one CI row, the scenario composer (P1) will need to evaluate rules in Python, and the variable-name check is something the C# loader cannot do at content time. Review again after the two-week mark of content work: if it has still caught no real error and the composer calls the C# CLI instead, delete `jsonlogic.py`, its test and the validate hook, and note it here.
