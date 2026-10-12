# p0-physiology-events-behaviour-s09 — What-if branches and built-in teaching events

Milestone: **M5 Content complete — all 17 P0 cases as data**

## What
All 47 walkthrough what-ifs as data branches {trigger, check_next, correct, incorrect, missed → consequence, escalation, expected response, next S1/S2, source, validation} with trigger types: value thresholds, trainee actions (e.g. allergen requested), omissions (missed after deadline), system (equipment fault, document missing, corridor SpO2 drop). Plus the built-in teaching events: wrong blood in tube, corridor deterioration, anaesthetist discovers hidden fact at Sign In, lab critical-result call.

## Why
Mistakes become learning moments exactly as the walkthrough writes them.

## How to implement
1. `Events/WhatIfRuntime.cs` loads `whatif_library.json` → Branch objects driven by the Event Director.
2. Bindings per case (p0-case-library-s03); T-E8: every what-if referenced by a P0 case is reachable.
3. Teaching events wired to their sources: wrong-blood (identity s01 flag + labelling), corridor (transport), Sign In discovery (checklist + surface-later), critical call (clinical-logic s08).
4. What-ifs needing P1 features are marked `requires: P1` and skipped — no fake behaviour.

## Verification
`dotnet test --filter FullyQualifiedName~WhatIf` → T-E8 green; each P0-bound what-if has a passing test.

## Files changed
`src/ProcSense.Core/Events/WhatIfRuntime.cs`, tests.

## Dependencies
p0-physiology-events-behaviour-s08; p0-case-library-s03.

## Common issues
Vasovagal faint and 'OT calls early' are not in the walkthrough — don't add them (Change Report).

## Sources
B-R14 Steps 7–8, T-E8; walkthrough Part 14, 14.4, 14.9; inventory D51, D219.
