# p0-case-library

**Tier:** P0 (17 cases; rest in p1-case-library-extended, SC-1..20 in p2) · **Builds on:** p0-clinical-logic, p0-medical-research-data · **Feeds:** scenarios, evaluation

> **Progress, 12 Oct 2026:** s02 (all 17 P0 cases play to their expected end state), s03 (what-if bindings), s04 (Stage B validator), s05 (mutation tests, 60 of 60 rejected) and s06 (adaptive difficulty, a rating and a lookup) are built and tested and approved by the owner (12 Oct 2026). s01's common header fields are not built because no case needs them yet (DT-095); the review-status gate and the manifest check are (ADR-0006).

## Goal
The 17 P0 patients as validated data with namespaced IDs, hidden facts + disclosure rules, what-if bindings, a contradiction
validator that rejects impossible cases, and deterministic difficulty selection.

## Scope
- **In:** patient/case schema, VAR-/STK-/SC- ID scheme (fixes WT-I01), case cards, Stage B validator (12 contradiction types) +
  mutation tests, what-if bindings, difficulty selection (disabled in assessment mode).
- **Out:** Composer solver + AI proposals (p1-scenario-composer-difficulty).

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Case source | Transcribed from walkthrough, each value checked against register | AI-generated | Clinically designed cases | — |
| IDs | VAR-/STK-/SC- + walkthrough label column | Walkthrough labels | ID collisions (WT-I01) | Supervisor prefers other names |
| Difficulty | Deterministic selection from approved pool | ML-based | Explainable, safe | — |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-CASE-1 | Validator rejects every contradiction type? | Mutation generator | 12/12 |
| EXP-CASE-2 | New case = zero code? | Add a case as data only | No code change |

## Exit
17 cases valid + golden-tested.

## Who does what
Developer: schemas, transcription, validator.

## Sources
Walkthrough Parts 12, 13, 14, D; Implementation Guide B-M2, B-M7, B-M19.
