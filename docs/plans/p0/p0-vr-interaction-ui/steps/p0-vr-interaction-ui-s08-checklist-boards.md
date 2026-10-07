# p0-vr-interaction-ui-s08 — WHO checklist board and ward checklist

Milestone: **M3 Walking skeleton 3 — OT Sign In / Time Out**

## What
WHO board bound to the SafetyChecklist observable: Sign In / Time Out (Sign Out in P1) items in order, pending → confirmed (green), STOPPED state in red with the reason, ProcSense extensions in a separate section; ward checklist panel with verify buttons.

## Why
Steps 10, 13–14 are led through these boards.

## How to implement
1. `Scripts/UI/Checklist/ChecklistBoard.cs` on the board's `SCREEN_WHOBoard` (greybox quad first).
2. The trainee directs each question to a role (menu) — wording from content only.
3. Each item shows who should answer (role icon); the trainee directs the question via a menu; NPC answers appear with voice.
4. STOPPED state shows the reason and what fixes it; resumes after the correction action (T-SC6).
5. Ward checklist panel: verify buttons show the state evidence (e.g. 'pregnancy test: negative 08:42').

## Verification
Play Mode test drives Sign In + Time Out through the board for VAR-S1-A; a deliberate wrong allergy answer shows STOPPED in red.

## Files changed
`Scripts/UI/Checklist/*`.

## Dependencies
p0-simulation-workflow-s16.

## Common issues
Board order from the `order` field.

## Sources
B-S10 Steps 4, 6; B-R15 Step 6.
