# p0-case-library-s03 — What-if bindings per case

Milestone: **M5 Content complete — all 17 P0 cases as data**

## What
Attach the walkthrough what-if branches relevant to each P0 case (e.g. allergen requested → hypersensitivity branch; unlabelled tube → sample rejected; unstable patient sent with porter only → corridor deterioration) with case-specific trigger overrides, and prove every binding is reachable from that case.

## Why
What-ifs bound to P0 cases are part of P0 teaching (original plan adjustment 4): each case must be able to show the mistakes the walkthrough describes for it.

## How to implement
1. `branches[]` in each case file (what-if IDs).
2. Reachability test per binding (T-E8).
3. Bindings stored in the case file `branches[]` with trigger overrides where the case needs them (e.g. allergen differs per case).
4. One headless test per binding forces the trigger and checks the branch's expected response/consequence.

## Verification
`dotnet test --filter FullyQualifiedName~WhatIfBindings` green for every P0 case; coverage report lists 0 unreachable bindings (T-E8).

## Files changed
`content/cases/*.json` (`branches[]`), `content/whatif_bindings.csv`, `tests/ProcSense.Core.Tests/WhatIfBindingTests.cs`.

## Dependencies
p0-physiology-events-behaviour-s09.

## Common issues
A binding whose trigger key doesn't exist in that case's state never fires — the reachability test catches it; fix the binding, not the engine.

## Sources
Implementation Guide B-M19 Step 3, B-R14 T-E8; walkthrough Part 14; inventory D219.
