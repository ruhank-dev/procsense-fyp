# p1-scenario-composer-difficulty

**Tier:** P1 · **Builds on:** p1-case-library-extended, p1-ai-language · **Feeds:** p2-content-extensions

## Goal
Compose complete, valid cases from approved building blocks with a deterministic constraint solver; let AI *propose* (Stage A) but
never approve; explain difficulty choices to the trainee.

## Scope
- **In:** building blocks (21 types) derived from walkthrough cases, constraint solver, Stage B validation (from P0), AI Stage A proposals,
  adaptive difficulty explanation text, case cards.
- **Out:** auto-approval of any generated case (never).

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Composition | Deterministic solver; AI proposes only | AI composes freely | Safety | — |
| Approval | ~~Every composed case needs medical approval before the pool~~ **Revised 12 Oct 2026 (ADR-0008):** composed and generated cases are practice-only (GENERATED_PRACTICE_ONLY) and gated by automated validation (Stage B, cross-modal rules, headless correct path, quality gate); clinician review is welcome but not a gate | Medical approval of every case (previous rule) | Owner policy: no additional clinician sign-off as a development gate; assessment keeps fixed cases | A generated case teaches something wrong that the validators missed |

## Experiments
| ID | Question | Method | Success metric | Status |
|---|---|---|---|---|
| EXP-COMP-1 | AI proposes 50 cases | Stage B filters | 0 auto-approved; valid ones stop at IMPLEMENTED | planned |
| EXP-COMP-2 | Solver produces ≥ 8 S1 / ≥ 6 S2 approved complete cases | Run + review | Met (Master Plan DoD) | planned |

## Live generation (12 Oct 2026)
This module's blocks (s01), solver (s02) and Stage A (s03) are reused live by `p1-live-scenario-generation` (stages LG-04 to LG-06); its lineage and step ids are unchanged. Feature ids: A1 (composer), A12 (recombination), E56 (difficulty explanation) in `docs/architecture/feature_registry.md`.

## Exit
Composer + Stage A working; difficulty explanations validated.

## Sources
Master Plan §28, §34; Implementation Guide B-M1, B-M7, B-R10.
