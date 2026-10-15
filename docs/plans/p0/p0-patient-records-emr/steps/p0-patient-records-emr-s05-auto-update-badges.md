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

## Implementation notes (15 Oct 2026): the Core half
- **Built in Core:** `src/ProcSense.Core/Records/Badges.cs` and `session.Records.Badges`: for each EMR tab, the documents that arrived during the session and have not been opened (`Count(tab)`, `Unseen(tab)`, `Counts`). A result document badges its tab the moment it is ready (the EMR's automatic update); a consent form or ward checklist created by signing badges the current episode; what the session starts with (the clerking note, the chart, the old record) is not news and has no badge. Opening a document anywhere, the EMR or the folder or the printout on the desk, clears it (`badge_cleared:<tab>` in the action's effects), so a badge never sticks. The state lives in Core and changes only through logged actions and the clock, so a replay gives the same badges.
- **Content:** `content/records/records.json` gains the EMR's tabs (profile, current episode, labs, imaging, reports, medical history, medications) and the tab of every document type (for example a laboratory report and a previous result under labs, a scan under imaging).
- **Critical results:** the laboratory still phones (p0-clinical-logic); the badge is in addition and never replaces the call (tested).
- **Tests:** `BadgesTests` (6), including the step's own check headless (VAR-S1-A liver tests: after the turnaround the labs badge shows 1, opening the result clears it, the log has `open_document`). Engine tests 1,226 pass; the golden runs are unchanged.
- **Not built (the Unity half, Sannan):** `TabBadge.cs` on the EMR's tab bar reading `Badges.Count(tab)`, and the Play Mode test. It also needs the EMR shell (p0-patient-records-emr-s03, Unity), on which this step depends. The step stays open until the Unity half is done.
