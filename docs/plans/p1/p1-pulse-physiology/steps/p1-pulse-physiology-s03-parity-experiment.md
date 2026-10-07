# p1-pulse-physiology-s03 — Parity experiment (EXP-PULSE-1, EXP-PULSE-2)

Milestone: **M11 P1 content — theatre extension, extended cases, composer, Pulse**

## What
Compare LitePhysio and Pulse on the same cases (Usman treated/untreated, VAR-S1-A baseline, an anaphylaxis what-if, dehydration): plot both, have the medical reviewer pick the more realistic, measure compute cost on Quest (≤ 3 ms/frame target) or server latency, and decide where Pulse is used.

## Why
Pulse may add realism but costs complexity and compute; the choice must be evidence-based.

## How to implement
1. Plots via the physiology plot tool; review; record decision.
2. Cases: Usman (treated/untreated), VAR-S1-A baseline, an anaphylaxis what-if, dehydration (VAR-S2-G).
3. Metrics: direction/timing agreement with walkthrough patterns, reviewer preference, compute cost (≤ 3 ms/frame on Quest per guide target, or server latency).

## Verification
Comparison plots + reviewer verdict + decision recorded in the README.

## Files changed
Plots under `docs/research/physio-plots/pulse-parity/`, `docs/plans/p1/p1-pulse-physiology/README.md` (decision).

## Dependencies
p1-pulse-physiology-s02.

## Common issues
Pulse responds to physiological inputs differently from our effect model — compare shapes and directions, not exact numbers.

## Sources
Implementation Guide B-R13 Step 6 (Pulse gate: ≤ 3 ms/frame), T-P6/T-P7; inventory D39.
