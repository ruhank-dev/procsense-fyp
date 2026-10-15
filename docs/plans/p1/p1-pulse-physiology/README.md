# p1-pulse-physiology

**Tier:** P1 · **Builds on:** p0-physiology-events-behaviour, p1-backend · **Feeds:** evaluation

## Goal
Add the Pulse Physiology Engine (Kitware, open source) as a higher-fidelity option behind the same `IPhysiologyEngine` interface,
with LitePhysio remaining the default/fallback.

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Where Pulse runs | Server-side (laptop) or desktop build | On Quest | Compute cost | EXP-PULSE-1 shows Quest can run it |
| Licence | Verify current Pulse licence before use (inventory says Apache-2.0, re-confirm) | — | Proof of truth | — |

## Experiments
| ID | Question | Method | Success metric | Status |
|---|---|---|---|---|
| EXP-PULSE-1 | Pulse vs LitePhysio parity on P0 golden runs | Same cases both engines | Same end states; medical reviewer prefers one | planned |
| EXP-PULSE-2 | Can Pulse run on Quest? | Native build test | Frame budget held | planned |

## Exit
Pulse selectable; parity report written.

## Sources
Implementation Guide B-R13 (Pulse gate); inventory D39.
