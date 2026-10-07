# p0-simulation-workflow-s09 — Steps 6–7: choosing tests and handling results

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
Workflow side of investigations: ordering against indication rules (APPROPRIATE / NOT INDICATED / MISSED), bedside labelling check, pregnancy-test gate, result viewing + interpretation + acknowledgement, and readiness blocks for unreviewed or unacknowledged critical results.

## Why
Walkthrough Steps 6–7: selective testing, bedside labelling and acting on results (incl. artefacts) are core lessons.

## How to implement
1. Verbs `order_test(test)` (requires identity — L1-02), `label_tube(tube)`, `view_result(test)`, `interpret(test, code)`, `acknowledge_result(test)`.
2. L6-01: indication rule from `test_indications.csv` → credit / minor item (shotgun ordering E6-A listed in debrief).
3. L6-02 / E6-B: tube not labelled at the bedside after identity touched → sample rejected after delay (E-SAMPLE-REJECT), re-take needed; pre-labelled tubes → wrong-blood event armed.
4. L6-05 / E6-D: eligible patient without pregnancy test → imaging/theatre blocked ('Pregnancy status required').
5. L7-01…L7-03: view → interpret against finding codes → act; unviewed required result = readiness block (E7-A); critical not acknowledged = hard blocker (T07-2, T07-WT2).
6. 'Appendix not visualised' interpreted as negative → major item (equivocal, not negative).

## Verification
`dotnet test --filter FullyQualifiedName~TestsWorkflow` → T06-1, T06-2, T06-5, T07-1, T07-2, T07-WT1, T07-WT2 green; VAR-S1-A golden orders LFTs + urine pregnancy test only → 0 'not indicated' items (walkthrough Scene 6).

## Files changed
`src/ProcSense.Core/Workflow/Steps/TestsSubsystem.cs`, tests.

## Dependencies
p0-clinical-logic-s06–s09; p0-simulation-workflow-s13 (transport for imaging).

## Common issues
Results only appear after turnaround (clinical-logic) — tests must time-skip, not sleep.

## Sources
Implementation Guide §12–13 (L6-01…L6-05, L7-01…L7-04, E6-A…E6-D, E7-A/B, T06/T07); walkthrough Steps 6–7, Part 5.
