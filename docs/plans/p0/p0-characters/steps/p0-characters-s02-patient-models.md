# p0-characters-s02 — Patient models for the 17 P0 cases

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

## What
Patient characters covering each P0 case's demographics (age, sex, build — e.g. Mrs Ayesha Khan 42 F, BMI 27; Mr Usman 34 M; Ms Sadia 24 F), in gown or own clothes per scenario, with body sockets for every bedside interaction: wrist (wristband, cannula), finger (SpO2), upper arm (BP cuff), chest + limbs (10 ECG electrodes), ear (thermometer), abdomen regions (examination hotspots), face (oxygen devices), legs (stockings), thigh (diathermy pad).

## Why
The patient is the centre of every interaction; sockets must match the equipment empties.

## How to implement
1. Generate/adjust from the chosen base; reuse models where demographics match (YAGNI).
2. Outfits (gown, own clothes) scripted in Blender with the same pipeline as equipment.
3. Socket empties named `SOCKET_<Part>` consistent with `equipment-interaction-spec.md`.
4. Case file references a `patient_model` ID.

## Verification
Every P0 case maps to a model; the pilot wristband, SpO2 probe and BP cuff snap to the right sockets.

## Files changed
`unity/Assets/_Project/Art/Characters/Patients/*`, outfit scripts.

## Dependencies
p0-characters-s01.

## Common issues
Keep shape keys ≤ 15 per mesh to limit skinning cost (B-S2).

## Sources
B-S2 Step 1; walkthrough Parts 12–13 (demographics).
