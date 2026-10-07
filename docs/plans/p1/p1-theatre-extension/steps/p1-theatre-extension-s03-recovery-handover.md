# p1-theatre-extension-s03 — Recovery handover in the reused patient bay

Milestone: **M11 P1 content — theatre extension, extended cases, composer, Pulse**

## What
After Sign Out, the patient is handed over to the recovery nurse in the patient bay layout (approved P6): SBAR including procedure, findings, analgesia/antiemetic plan, antibiotic plan, drains, concerns; omissions scored.

## Why
Handing the patient safely to recovery closes the journey the walkthrough describes (Step 15) and practises a second, different SBAR audience.

## How to implement
1. Bay manifest variant `recovery`; recovery nurse avatar (P1 role).
2. Required elements from content; handover subsystem reused.
3. Recovery nurse avatar (from the chosen human set) with role uniform.
4. Required handover elements from content; omissions scored like Step 12.

## Verification
Recovery handover playable for S1 and S2; omission fixtures scored.

## Files changed
`unity/Assets/_Project/Data/room_manifest_bay_recovery.json`, `content/handover_required.json` (recovery section), recovery nurse prefab, tests.

## Dependencies
p1-theatre-extension-s01.

## Common issues
Recovery handover reusing Step 12 logic must use recovery-specific required elements (analgesia, antiemetic, antibiotic plan, drains).

## Sources
Walkthrough Step 15 (handover to recovery); Implementation Guide B-S12 Step 3; phase review P6.
