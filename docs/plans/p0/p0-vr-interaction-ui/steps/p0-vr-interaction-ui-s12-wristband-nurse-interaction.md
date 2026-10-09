# p0-vr-interaction-ui-s12 — Wristband interactions: report, nurse hand-over, tablet fallback, missing band

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

Added 09 October 2026 at the project owner's request ([deferred-tasks.md](../../../../deferred-tasks.md) DT-007, DT-009).

## What
Everything the trainee does with wristbands in VR, including the failure paths:

1. **Read the band (normal)**: grab or point at the wrist, rotate to read name / DOB / MRN (text from the Core observable) — already in p0-vr-interaction-ui-s01.
2. **Tablet fallback (T01-5)**: if the band cannot be grabbed (VR failure, accessibility, seated play) a **"Read wristband"** button on the tablet's Admission tab shows the same card. It emits the same `read_wristband` action, so Core, log and replay do not know the difference.
3. **Report mismatch**: the "Report mismatch" button (tablet) or pointing at the band and choosing "Report problem". Core starts the nurse task; the tablet shows "Nurse called".
4. **Nurse hand-over**: while the nurse replaces the band the trainee sees the nurse (p0-characters-s08) and a status chip ("Nurse at bedside — checking records"); when the Core says `DONE` the trainee **reads the new band again** and presses **Confirm identity**.
5. **Missing band**: the wrist shows no band; tablet shows "No wristband on patient" and a **"Ask nurse to apply a band"** button (`request_wristband`). The allergy band, when the case has one, is visible after it is applied.
6. **Feedback**: red banner "Identity not confirmed" until verified (practice mode), green tick after; rejected actions show their reason text from the Core (`ST1_MISMATCH`, `WRISTBAND_MISSING`, `IDENTITY_NOT_CONFIRMED`).

## Why
The owner wants the mismatch workflow to be a real interaction, not a hidden state; the tablet alternative must exist so no player is blocked by a grab problem (Guide T01-5).

## How to implement
1. Views read `ObservableState` only: `IdentityCardView` (extend), `NurseStatusChip`, `WristbandSocketView`. Emitters: `TabletReadWristbandEmitter`, `ReportMismatchEmitter`, `RequestWristbandEmitter`.
2. Button enabled/disabled strictly from Core state (never from local flags).
3. All wording from Core/content (no clinical text in Unity).
4. Accessibility: the tablet path must be reachable seated, with large text (ties into p0-vr-interaction-ui-s10).
5. Play Mode tests: (a) grab path and tablet path produce the **same log hash**; (b) mismatch fixture → nurse → re-read → confirm; (c) missing-band fixture; (d) hash equals the headless golden from p0-simulation-workflow-s18.

## Verification
Device simulator: complete Step 1 on the normal, mismatch and missing-band fixtures using only the tablet, then again by hand in the headset.

## Files changed
`unity/Assets/_Project/Scripts/Views/*`, tablet prefab (Admission tab), emitters, Play Mode tests.

## Dependencies
p0-vr-interaction-ui-s01, p0-vr-interaction-ui-s03 (tablet shell), p0-simulation-workflow-s18, p0-characters-s08.

## Common issues
Band text unreadable at arm's length → font size/contrast (EXP-UI-2); two buttons doing the same job confuse players → label the tablet one clearly as the alternative.

## Sources
Guide §07 test T01-5 and UI paragraph (tablet Admission tab, ID compare card); equipment-interaction-spec.md (wristband row); owner instruction 09 October 2026.
