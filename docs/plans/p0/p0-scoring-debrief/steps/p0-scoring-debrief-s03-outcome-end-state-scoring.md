# p0-scoring-debrief-s03 — Outcome and end-state scoring

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
The final result combines the end state (READY_SAFE, POSTPONED_APPROPRIATELY, REDIRECTED_APPROPRIATELY, SAFE_BUT_DELAYED, SAFETY_FAILURE), critical items, lateness and category scores. Any CRITICAL item applies the fail gate. Appropriate postponement counts as success. (The guide's older outcome names ready-stable / not-ready etc. are replaced by these five — Change Report.)

## Why
The trainee's result must say what the walkthrough says: postponing correctly is success, a violated blocker is a safety failure, avoidable delays are noted — otherwise trainees learn the wrong lesson from the score.

## How to implement
1. `Scoring/OutcomeScorer.cs` → `ScoreReport {process_by_category, end_state, critical[], items[], lateness[]}`.
2. Safety-failure rules from content (e.g. never doing the pregnancy test in VAR-S2-B; consenting a confused patient; ignoring a critical result).
3. Multiple criticals all listed (T-SCO4).

## Verification
`dotnet test --filter FullyQualifiedName~Outcome` → T-SCO2, T-SCO4 green; every end state scores as defined in fixtures.

## Files changed
`src/ProcSense.Core/Scoring/OutcomeScorer.cs`, tests.

## Dependencies
p0-simulation-workflow-s17.

## Common issues
Double-counting the same mistake as several items → dedupe by rule + evidence.

## Sources
B-M15 Steps 3, 6, T-SCO2/4; walkthrough Part 16; Change Report §B.4.
