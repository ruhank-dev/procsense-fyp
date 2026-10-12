# p0-case-library-s06 — Difficulty modes and next-case selection

Milestone: **M5 Content complete — all 17 P0 cases as data**

## What
Beginner vs advanced mode (beginner: NPC safety nets — nurse challenges, prompts; advanced: consequences play out, e.g. reaction branches — walkthrough Step 8) and deterministic next-case selection: Elo-style ratings per competency (K-factor 16, ≥ 3 sessions before adapting), 8 difficulty dimensions mapped to case tags, approved pool only, practice only.

## Why
Beginners need safety nets (the nurse challenges an unsafe request) while advanced trainees must experience consequences; adaptive selection gives each trainee the cases that train their weakest competencies — but assessment must stay identical for everyone.

## How to implement
1. `Difficulty/Ratings.cs` (per trainee pseudonym), `Selector.cs`; `content/difficulty/dimensions.json`.
2. Mode flag consumed by the medicine workflow (challenge vs reaction).
3. CLI `procsense-sim next-case --history <dir>` for testing.

## Verification
`dotnet test --filter FullyQualifiedName~Difficulty` → T-DF1…T-DF4 green; assessment mode returns the fixed order.

## Files changed
`src/ProcSense.Core/Difficulty/*`, `content/difficulty/dimensions.json`, tests.

## Dependencies
p0-scoring-debrief-s03.

## Common issues
New trainees have no ratings — use the default curriculum order until 3 sessions exist (T-DF2).

## Sources
Implementation Guide B-M7 Steps 1–3, 5, T-DF1–T-DF4.
