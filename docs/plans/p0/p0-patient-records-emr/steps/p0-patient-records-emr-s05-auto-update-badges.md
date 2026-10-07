# p0-patient-records-emr-s05 — Automatic updates and badges

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
New results appear instantly in the right section with a badge on its tab; the badge clears when opened.

## Why
User direction: the computer updates automatically when tests complete.

## How to implement
1. Subscribe to ResultReady; badge state per tab.
2. `EmrTabBadges` keeps unseen document IDs per tab; ResultReady adds, opening the document removes; state lives in Core Records (so replay reproduces it), not only in Unity.
3. Critical results also flash the phone; the EMR badge never replaces the call.
4. Play Mode test: send VAR-S1-A LFTs → after turnaround the Labs tab shows a badge with count 1 → open → badge cleared → log contains `open_document`.

## Verification
Play Mode test green; in the headset the Labs badge appears when the patient returns from the lab door and clears when you open the result.

## Files changed
`src/ProcSense.Core/Records/Badges.cs` (seen/unseen state), `unity/Assets/_Project/Scripts/Records/Emr/TabBadge.cs`, Play Mode test.

## Dependencies
p0-patient-records-emr-s03.

## Common issues
Badges that never clear (e.g. document opened in the folder, not the EMR) → define 'seen' as opened anywhere; Core tracks it.

## Sources
User direction (computer updates automatically after tests); p0-diagnostic-transitions README loop.
