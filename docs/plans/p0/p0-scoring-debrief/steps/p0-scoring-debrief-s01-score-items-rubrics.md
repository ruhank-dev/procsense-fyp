# p0-scoring-debrief-s01 — Score item types and rubrics

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Score items created from rule hits and goal events: types REQUIRED, CONTRAINDICATED, TIMING, ORDER, RESPONSE, APPROPRIATENESS, INTERPRETATION, COMMUNICATION, CRITICAL; each `ScoreItem {id, category, type, severity, status, points, evidence[seq], rule_id, source}`. Rubric weights per category per scenario in content (provisional until reviewed), with an 'acceptable alternatives' list per item.

## Why
Fair judgement needs explicit, reviewable rubrics — never hard-coded weights.

## How to implement
1. `Scoring/ScoreItem.cs` + factory from rule hits / goal events.
2. `content/scoring/rubrics_S1.json`, `rubrics_S2.json` (category weights, `provisional: true`).
3. Dedupe by rule + evidence (guide troubleshooting: double-counted items).
4. Walkthrough scoring rules as content: teaching moments carry no penalty (e.g. wrong ASA corrected by the anaesthetist); professionalism items (examining without permission, pressuring a refusal).
5. Item IDs stable across versions (debrief history and evaluation depend on them).

## Verification
`dotnet test --filter FullyQualifiedName~ScoreItem` green; T-SCO3 category totals match the rubric.

## Files changed
`src/ProcSense.Core/Scoring/ScoreItem.cs`, `content/scoring/*.json`, tests.

## Dependencies
p0-core-engine-s04; p0-clinical-logic-s01.

## Common issues
Score changes unexpectedly → golden file diff in the PR shows why.

## Sources
Implementation Guide B-M15 Steps 1–2, 6, T-SCO3; walkthrough Part 16.
