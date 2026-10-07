# p0-simulation-workflow-s05 — Case graph: goals, deadlines, decision points, readiness blockers, hints

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
The engine's 'what must happen' layer (B-R12 Steps 1–7). Content defines **goals** (e.g. 'allergies known'), **decision points** (a choice card such as DP-S1-FAST: proceed vs inform anaesthetist & delay) and **readiness blockers** (checks before transfer / Sign In / Time Out). The engine instantiates the goals that apply to the case, re-evaluates them after every action/tick, applies deadline misses, exposes decision cards and blocks unsafe progress.

## Why
This is how the simulator knows what the trainee has done, what is still missing, and what is unsafe to do next — for every case, without case-specific code.

## How to implement
1. Content schema (`content/schema/workflow.schema.json`): `case_goal {id, step, type, applies_if, complete_if, deadline_min?, on_missed{effects[], events[]}, score_items[]}`, `decision_point {id, step, trigger, options[{id, label, next_state_patch, events_enable[], score_item}], expected[], sources[]}`, `readiness_blocker {id, check_at[verbs], blocked_if, message}` — all conditions in the rule language from p0-clinical-logic-s01.
2. `Workflow/CaseGraph.cs`: load + validate; detect cycles in hard-order edges (load error names the cycle — T-CG2).
3. Goal instantiation at session start (`applies_if` vs case card); status PENDING → COMPLETE / MISSED with sim timestamps.
4. Goal evaluation after each Apply/Tick using a **dirty-key index** (map each goal to the state keys its rule reads, extracted at load) so only affected goals re-evaluate (T-CG6: 200 goals < 0.3 ms per Apply).
5. Deadlines on the sim clock: missed → apply `on_missed` effects/events + score item; deadline exactly on a tick boundary is deterministic (T-CG3).
6. Decision points: trigger → card in ObservableState; verb `decide(dp, option)` applies the patch and logs expected vs chosen (T-CG5).
7. Readiness blockers evaluated before the listed verbs; rejected with the blocker message (practice shows it, assessment logs it) — e.g. transfer without consent (T-CG4).
8. Hints: practice mode + difficulty ≤ 2 only → next suggested goal; none in assessment.

## Verification
`dotnet test --filter FullyQualifiedName~CaseGraph` → T-CG1…T-CG6 green. CLI: `task sim -- run --case VAR-S1-A --seed 42 --actions tests/golden/skeleton1_correct.actions.json` → observable shows goal statuses; removing the allergy question leaves G-ALLERGY PENDING.

## Files changed
`src/ProcSense.Core/Workflow/CaseGraph.cs`, `Goals.cs`, `DecisionPoints.cs`, `ReadinessBlockers.cs`; `content/schema/workflow.schema.json`; `content/workflow/goals.json`, `decision_points.json`, `blockers.json`; tests.

## Dependencies
p0-clinical-logic-s01 (rule evaluator), p0-core-engine-s06 (modes).

## Common issues
Goal never completes → its rule reads a state key that doesn't exist (check key names at load and fail loudly). Every blocker must have a message (validator enforces).

## Sources
Implementation Guide B-R12 Steps 1–7, tests T-CG1–T-CG6; walkthrough Parts 3, 11, 16.
