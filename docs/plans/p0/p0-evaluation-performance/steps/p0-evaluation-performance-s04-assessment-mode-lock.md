# p0-evaluation-performance-s04 — Assessment-mode lock

Milestone: **M9 P0 release**

## What
Assessment mode loads only APPROVED_FOR_ASSESSMENT content, disables hints and difficulty adaptation, uses the fixed event schedule, and logs critical items strictly; tested with an unapproved case/value.

## Why
Assessment results are only meaningful if every trainee faces exactly the same approved content, with no hints and no adaptation — one leaking practice feature would invalidate the assessment.

## How to implement
1. Integration test across loader, events, difficulty and UI hints.
2. Single integration test `AssessmentModeTests` runs a session in assessment mode with: one case value marked SOURCE_IDENTIFIED (not approved) → loader refuses with the item ID; an event below assessment status → not loaded (T-E6); difficulty selection → returns the fixed order (T-DF3); hints → absent from ObservableState.
3. Unity: mode chosen only from the main menu (not changeable mid-session); the mode is written into the session log header and shown on the debrief.
4. Practice-only UI (hint chip, consent element tracker, nurse prompts) hidden in assessment via one `Mode` check in the views.

## Verification
`dotnet test --filter FullyQualifiedName~AssessmentMode` green; a VR session started in assessment mode shows no hints and its log header says `mode: ASSESSMENT`.

## Files changed
`tests/ProcSense.Core.Tests/AssessmentModeTests.cs`, mode check in `unity/Assets/_Project/Scripts/UI/ModeGate.cs`, log header field `mode`.

## Dependencies
p0-core-engine-s06.

## Common issues
Practice-only UI leaking into assessment through a missed view → one central mode check, tested.

## Sources
Master Plan §27 (loader refuses unapproved content in assessment); Implementation Guide B-M11 mode minimums, T-CL-2; B-R14 T-E6; B-M7 T-DF3.
