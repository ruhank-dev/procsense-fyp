# p0-clinical-logic-s14 — Problem registry, priority ladder, golden rules

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Every abnormality detected by subsystems registers a Problem {type: LIFE_THREAT | SAFETY_BLOCKER | TIME_CRITICAL | PATHWAY_CHANGING | OPTIMISABLE | INFORMATION_ONLY, ladder_rank 1–7, part_d_priority P1–P4, clock?, resolved?}; ordered by the 7-step ladder (14B.1); golden rules applied (most dangerous sets the pace; S1 problems add up to postpone; S2 to parallel preparation; new diagnosis beats old plan; handover lists all problems — 14B.2).

## Why
Stacked cases are about choosing what to do first.

## How to implement
1. `Clinical/ProblemRegistry.cs` from `problem_types.csv`, `priority_ladder.csv`, `priority_scale_part_d.csv`.
2. Life threat → case switches to emergency mode (workflow interrupt).
3. Deterministic ordering: ties broken by content order.
4. Information-only problems hidden from UI but kept for handover (B-R17 troubleshooting).
5. Test T-DE1 with the guide's STK-S1-A example (verify ranks against Part D in research).

## Verification
`dotnet test --filter FullyQualifiedName~ProblemRegistry` → T-DE1 green; all P0 stacked cases rank as the walkthrough.

## Files changed
`src/ProcSense.Core/Clinical/ProblemRegistry.cs`, tests.

## Dependencies
p0-clinical-logic-s01; p0-medical-research-data-s05.

## Common issues
Ladder and types are Type B — the medical reviewer may adjust; keep them in content.

## Sources
B-R17 Steps 1–3, T-DE1; walkthrough 14B.1, 14B.2, Part D; inventory D212.
