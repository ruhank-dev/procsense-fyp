# p2-content-extensions

**Tier:** P2 · **Builds on:** p1-case-library-extended, p1-scenario-composer-difficulty

## Goal
- The 20 compact stacked cases **SC-1..SC-20** (walkthrough Part 14B) as playable data.
- New case combinations using the walkthrough's **14B.5 recipe** via the composer, each medically approved.
- Terminology codes: ICD-11 for diagnoses/comorbidities, LOINC for lab tests (stable IDs, interoperability).
- Local realism: drug availability check (DRAP Pakistan); DailyMed as backup drug-label source.

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Compact cases | Expand to full case format with composer help | Keep compact format | One runtime format | — |
| Codes | Add as extra columns, not replacing our IDs | Replace IDs | Non-breaking | — |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-P2C-1 | Can each SC case be expanded without new engine features? | Attempt all 20 | Count needing changes |

## Exit
SC-1..SC-20 playable; codes mapped; licences (LOINC registration, ICD-11 terms) recorded.

## Sources
Walkthrough Part 14B; inventory D25, D27, D29, D30, D483.
