# p0-simulation-workflow-s07 — Step 4: examining the patient

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Examination verbs reveal the case's finding codes with the patient's reaction: ask permission (+ chaperone where required), examine regions (general, eyes for jaundice, heart, lungs, abdomen regions, Murphy's sign in S1, RLQ signs in S2), airway check (mouth opening, Mallampati, neck, thyromental) stored for Step 5 and Sign In.

## Why
Walkthrough Step 4: examination findings change the pathway (Murphy's positive → acute branch; rigid abdomen → senior now) and airway findings feed anaesthesia.

## How to implement
1. `Workflow/Steps/ExaminationSubsystem.cs`: verbs `ask_exam_permission`, `examine(region)`, `murphy_test`, `airway_check(item)`; regions/findings from the case (`exam_findings`).
2. L4-01: examining without permission → professionalism evidence + trust −1 (E4-A).
3. L4-03 / E4-B: painful region first → pain +1, trust −1 (values in `content/behaviour.json`, Type D).
4. L4-02 / E4-C: Murphy's not checked in a febrile S1 patient → finding stays hidden → debrief item (walkthrough 'misses').
5. L4-04: airway findings → `clinical.airway{}`; not passed on → Sign In challenge later.
6. Variant goal: rigid board-like abdomen (S2 generalised) → 'senior now' goal active (T04-5).

## Verification
`dotnet test --filter FullyQualifiedName~Examination` → T04-1…T04-5 green; VAR-S1-A reveals 'eyes white, heart/lungs normal, soft tummy, mild RUQ tenderness on deep pressure, Murphy's negative, mouth 3 fingers, Mallampati 2' exactly as walkthrough Part 12 Scene 4.

## Files changed
`src/ProcSense.Core/Workflow/Steps/ExaminationSubsystem.cs`, case `exam_findings`, tests.

## Dependencies
p0-simulation-workflow-s05; p0-case-library-s01 (exam_findings field).

## Common issues
Hotspot visuals are VR work — Core only knows region IDs. Chaperone requirement is institutional (content flag).

## Sources
Implementation Guide §10 (L4-01…L4-04, E4-A…E4-C, T04-1…T04-5); walkthrough Part 11 Step 4, Part 12 Scene 4.
