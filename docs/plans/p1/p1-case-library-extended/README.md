# p1-case-library-extended

**Tier:** P1 · **Builds on:** p0-case-library · **Feeds:** p1-scenario-composer-difficulty, p1-evaluation

## Goal
The remaining walkthrough cases as data, each golden-tested:
- **Variants (10):** VAR-S1-I, -K, -L, -M · VAR-S2-E, -H, -I, -J, -K, -L
- **Stacked (6):** STK-S1-A, -B, -C · STK-S2-A, -C, -D

## Scope
- **In:** transcription + register check, what-if bindings, any new content types they need (e.g. maternity early-warning for VAR-S2-H,
  minor/guardian consent for VAR-S2-I, capacitous refusal for VAR-S2-L), new equipment if required.
- **Out:** SC-1..SC-20 (P2).

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Approach | Content-only where the engine already supports it; engine additions first where it doesn't | Mixed | Keeps engine changes visible | — |

## Experiments
| ID | Question | Method | Success metric | Status |
|---|---|---|---|---|
| EXP-CASE-3 | How many P1 cases need engine changes? | Check each against current schema | Count + list of new features | planned |

## Exit
All 16 cases valid, golden-tested, playable in VR.

## Sources
Walkthrough Parts 12, 13, D; Data Source Inventory D459–D482.
