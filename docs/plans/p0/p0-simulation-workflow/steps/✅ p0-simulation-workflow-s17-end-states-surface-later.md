# p0-simulation-workflow-s17 — End states and late-surfacing consequences

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
End-state resolver (READY_SAFE, POSTPONED_APPROPRIATELY, REDIRECTED_APPROPRIATELY, SAFE_BUT_DELAYED, SAFETY_FAILURE) and the generic mechanism that makes **unasked hidden facts surface at their walkthrough moment** (e.g. anaesthetist discovers this-morning apixaban at Sign In → case stops).

## Why
Postponing correctly is success; questions the trainee skipped must have consequences.

## How to implement
1. `Workflow/EndStateResolver.cs` from `content/end_states.csv` (meaning per state, walkthrough Part 16).
2. SAFE_BUT_DELAYED needs a definition of 'avoidable delay' → Type D value in content (register row).
3. SAFETY_FAILURE: any blocker violated or life-threatening event missed (rules from content).
4. Surface-later: each hidden fact may carry `disclose_on {step/event, by_role}` in the case file; the engine reveals it then and applies its consequence.
5. Tests: one fixture per end state; T-CG7 (VAR-S1-PB → POSTPONED/redirected per walkthrough), T-CG8 (VAR-S2-B → EMERGENCY then REDIRECTED to gynaecology), T02-WT1.

## Verification
`dotnet test --filter FullyQualifiedName~EndState` green — all five end states reachable; T-CG7, T-CG8, T02-WT1 pass.

## Files changed
`src/ProcSense.Core/Workflow/EndStateResolver.cs`, tests.

## Dependencies
p0-simulation-workflow-s16; p0-case-library-s01 (`disclose_on`).

## Common issues
Keep end-state logic data-driven; never `if case == ...`.

## Sources
B-R12 Step 10 (T-CG7, T-CG8); §08 T02-WT1; walkthrough Part 16, Part 11 Step 2.
