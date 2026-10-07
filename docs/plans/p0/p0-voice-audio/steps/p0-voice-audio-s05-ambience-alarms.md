# p0-voice-audio-s05 — Ambience, equipment sounds and alarms (EXP-VOX-2)

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

## What
Room ambience (ward, ED, OT), equipment sounds (cuff inflation, suction, pump, phone ring, insufflator), and monitor alarm sounds tied to Core alarm states (priority levels), with a sound list recording each source.

## Why
In a real ward and theatre, sound carries information: alarms signal danger, equipment sounds confirm actions. Alarms must follow Core state exactly so trainees learn to react to them correctly.

## How to implement
1. `Scripts/Audio/AlarmAudioView.cs` subscribed to Observable alarms.
2. Sound list `docs/research/sounds.csv` (source, author) for attributions.
3. EXP-VOX-2: medical reviewer checks alarm behaviour.
4. Alarm priorities (low/medium/high) mapped from Core alarm states; high-priority alarm interrupts ambience ducking.
5. Spatial audio on device sources (monitor, pump, suction) with sensible rolloff.
6. Acknowledge/silence on the monitor stops the sound but logs the acknowledgement.

## Verification
Play Mode test: SpO2 below threshold fixture triggers the alarm sound; acknowledge silences it and logs the action; reviewer accepts realism (EXP-VOX-2).

## Files changed
`Scripts/Audio/*`, `Audio/*`, `docs/research/sounds.csv`.

## Dependencies
p0-vr-interaction-ui-s04.

## Common issues
Alarm fatigue: continuous loud alarms annoy testers — follow real priority patterns and allow acknowledge; never mute without logging.

## Sources
Implementation Guide B-S10 Step 2 (monitor alarms); walkthrough Part E (vital-sign monitor: alarms at set thresholds); inventory D81.
