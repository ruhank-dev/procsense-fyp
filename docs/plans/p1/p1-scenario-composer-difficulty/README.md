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
| Approval | Every composed case needs medical approval before the pool | Validator-only approval | Medical content rule | — |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-COMP-1 | AI proposes 50 cases | Stage B filters | 0 auto-approved; valid ones stop at IMPLEMENTED |
| EXP-COMP-2 | Solver produces ≥ 8 S1 / ≥ 6 S2 approved complete cases | Run + review | Met (Master Plan DoD) |

## Exit
Composer + Stage A working; difficulty explanations validated.

## Sources
Master Plan §28, §34; Implementation Guide B-M1, B-M7, B-R10.
