# p0-simulation-workflow-s12 — Step 10: preparing the patient (ward/ED checklist)

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Pre-operative checklist where each tick is **verified against state**, not just clicked: gown, jewellery removed/taped, dentures, stockings, temperature, pregnancy test documented, consent present, ID band, documents together, site marking per policy; signing it enables transfer.

## Why
Walkthrough Step 10: missing documents block transfer; late fasting discoveries must reach the anaesthetist.

## How to implement
1. `Workflow/Steps/WardChecklistSubsystem.cs`; items from `content/ward_checklist.json` with a `verify_if` rule each.
2. L10-01: tick contradicting state → item + discrepancy event (E10-A 'fasted' ticked during a breach).
3. L10-02: `sign_ward_checklist` only when all ticked → transfer enabled (state flag read by the transport blocker).
4. Late discovery ('tea with milk at 7') → must be passed to anaesthetist (goal).
5. Razor used instead of clippers → infection-risk debrief item; jewellery left on → item (E10-B, clinical validation flag kept).

## Verification
`dotnet test --filter FullyQualifiedName~WardChecklist` → T10-1, T10-2 green; VAR-S1-A Scene 9 list completes in the golden run.

## Files changed
`src/ProcSense.Core/Workflow/Steps/WardChecklistSubsystem.cs`, `content/ward_checklist.json`, tests.

## Dependencies
p0-simulation-workflow-s05.

## Common issues
Site marking 'not applicable' for midline surgery is institutional (content), not code.

## Sources
Implementation Guide §16 (L10-01/02, E10-A/B, T10-1/2); walkthrough Step 10, Part 12 Scene 9.
