# p0-vr-interaction-ui-s01 — Skeleton 1 interactions

Walking skeleton 1 · step 14 of 15 · blocked until p0-vr-platform-s03 and p0-environment-rooms-s01

## What
Make Steps 1–3 playable in the greybox bay:
- **Tablet** (hand-held): "Talk" menu (introduce yourself, ask name + DOB, read name to patient, ask allergies, ask what happened, ask why here,
  ask responsible adult, history topics, summarise) → patient answer shown as subtitle + added to the clerking note panel; "Confirm identity" /
  "Report mismatch" buttons enabled per Core state.
- **Wristband**: grab/point at the wrist → identifier card shows the band text (from Core observable).
- **Notes folder**: grab → simple panel with the notes identifiers (full folder/page-flip comes in p0-patient-records-emr).
- **Monitor**: SpO2 probe and BP cuff placeholders snap into sockets on the patient; "Measure" button on the monitor; values appear only when Core reports them.
- **Thermometer, RR count, ACVPU, pain**: tablet buttons for skeleton 1.
- Rejected actions show a short hint (e.g. "Identity not confirmed").

## Why
This is the first time a person plays the real simulation in VR.

## How to implement
1. Views: `TabletView`, `SubtitleView`, `ClerkingNoteView`, `IdentityCardView`, `MonitorView` — all read `ObservableState` only.
2. Emitters on each placeholder (SocketEmitter on probe/cuff sockets, UIButtonEmitter on tablet buttons).
3. All text shown comes from Core/content (no strings with clinical content in Unity).
4. Play Mode test drives the same sequence as `golden/skeleton1_correct` through the emitters and compares the log hash.

## Verification
Device simulator: complete Steps 1–3 using only tablet buttons and sockets; Play Mode golden test passes. Headset: same sequence by hand.

## Files changed
`unity/Assets/_Project/Scripts/Views/*`, tablet prefab, placeholder prefabs with emitters, Play Mode test.

## Dependencies
p0-vr-platform-s03, p0-environment-rooms-s01, p0-simulation-workflow-s04.

## Common issues
Text unreadable on Quest → raise font size (start of EXP-UI-2); sockets too fussy → enlarge snap radius.

## Sources
Implementation Guide B-S10 Step 1 (tablet), B-S9 Step 7 (wristband, sockets), B-S7 (emitters/views); walkthrough Part 16 Stages 1–3.

## Carried-over requirements (added 09 October 2026)

- The **tablet "Read wristband" button** (alternative when the band cannot be grabbed, test T01-5) and the nurse/missing-band interactions are specified in [p0-vr-interaction-ui-s12](p0-vr-interaction-ui-s12-wristband-nurse-interaction.md) (DT-008). Build the fallback button here if skeleton 1 needs it first.
