# p0-vr-interaction-ui-s06 — Bedside equipment interactions

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

## What
Implement every patient-bay row of `equipment-interaction-spec.md`: wristband rotate-to-read, allergy band, bed head-up/flat, monitor attach, BP cuff wrap + measure, SpO2 clip, tympanic thermometer, 12-lead ECG electrode placement + printout, stethoscope auscultation audio per site, glucose meter, blood sampling + bedside tube labelling + send to lab, cultures, urine/pregnancy test with timed result, cannula, fluids, pump, oxygen devices + flowmeter, medication trolley/drug chart, sharps, stockings/IPC, clippers vs razor, wheelchair/trolley transport choice, folder, consent form.

## Why
Steps 1–10 happen at the bedside with the trainee's own hands: identity bands, sensors, examination, sampling, IV access, oxygen, consent, preparation — each interaction must behave as the walkthrough describes, or the training transfers badly.

## How to implement
1. One spec row at a time: state machine data + emitters + view; test in the device simulator; tablet fallback each.
2. Specific interactions from B-S9 Step 7: rotate wrist to read band; count breaths from the avatar; pre-labelling possible (→ event); clippers vs razor; tape ring; stockings.
3. Order of work: identity (#1, #2) → monitoring (#9–#15) → sampling (#16–#19) → access/fluids/oxygen (#25–#28) → medicines + safety (#32–#35) → transport (#4, #38) → documents (#5, #7).
4. Each row gets a Play Mode test emitting the same SimActions as the hand interaction.

## Verification
Every bay spec row works in the headset; T-IN1 states in order.

## Files changed
`Scripts/Interaction/Bedside/*`, `content/equipment/*.json`.

## Dependencies
p0-vr-interaction-ui-s02; p0-blender-equipment-s06–s10.

## Common issues
Small items (tubes, probes) hard to grab with controllers → larger grab colliders than visuals, snap-to-hand poses; hand tracking tested in EXP-VR-2.

## Sources
equipment-interaction-spec.md (bay rows); Implementation Guide B-S9 Steps 5–7, T-IN1; walkthrough Steps 1–10, Part E.
