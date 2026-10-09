# p0-simulation-workflow

**Tier:** P0 (Steps 1–14; Step 15 in p1-theatre-extension) · **Builds on:** p0-core-engine, p0-medical-research-data · **Feeds:** scoring, scenarios, vr-interaction-ui

## Goal
The patient journey as data-driven logic: 15 walkthrough steps, S1/S2 20-stage scenario maps, blockers, hidden facts revealed
only when asked (and "surfacing later" if never asked), WHO checklist (exact 2009 wording + labelled extensions), ward/ED checklist,
locations + transport, SBAR handover structure, five end states.

## Scope
- **In:** case graph with parallel tracks and interrupts, unlock rules (no tests/medicines before identity confirmed), readiness blockers,
  disclosure engine, WHO Sign In / Time Out engine, transport modes (walk / wheelchair / trolley + escort + monitoring, chosen by the trainee and scored against patient stability) + escort rule, end-state resolver.
- **Also in (added 09 Oct 2026):** nurse-assisted wristband mismatch / missing-band resolution (s18) and robust spoken-identity validation (s19).
- **Out:** clinical thresholds (clinical-logic), vitals over time (physiology), visuals.

## Deliverables
Case graph engine + Step/FlowStage schema; checklist engine; transport subsystem; end-state resolver; golden "correct path" scripts.

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Canonical IDs | Part 11 steps; Parts F/G stages as per-scenario order | One linear list | Resolves WT-I09 numbering mismatch | — |
| Flow model | Step graph + parallel tracks + interrupts | Strict state machine; behaviour tree | S2 overlaps treatment and preparation | Too complex → hierarchical state machine |
| Checklist wording | Exact WHO 2009 text; extensions shown separately | Paraphrase | Walkthrough Part 15 | Institution's modified checklist |
| Skipped checklist item | Challenge, then stop case on repeat | Instant fail | Walkthrough | — |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-WF-1 | All 40 S1/S2 stages expressible as data? | Encode both maps | 0 stages need custom code |
| EXP-WF-2 | Late-surfacing hidden facts behave as written? | Golden runs skipping each question | Expected late stop every time |

## Exit
Headless golden runs for every P0 case reach the expected end state; every blocker tested.

## Who does what
Claude: all logic + tests.

## Sources
Walkthrough Parts 3, 11, 15, 16, F, G; Implementation Guide B-R12, B-R15, B-R16, step guides 01–14.
