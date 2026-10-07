# p0-voice-audio-s04 — Lip sync

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

## What
uLipSync on patient and staff faces with a viseme profile calibrated on the chosen TTS voices.

## Why
Characters that speak without moving their mouths look broken in VR and pull trainees out of the scenario; lip sync makes patient and staff dialogue believable.

## How to implement
1. Configure per character; calibrate profile; profile CPU cost.
2. Install uLipSync from its repository (record version); one `uLipSync` component per speaking avatar fed by the line's AudioSource; `uLipSyncBlendShape` maps phonemes (A/I/U/E/O/N) to the face's viseme blend shapes.
3. Calibrate a profile per TTS voice family (male/female) using 10 rendered lines; store profiles in `Audio/LipSync/`.
4. Profile CPU cost with 2 simultaneous speakers on Quest 2; disable lip sync on LOD1+ characters.

## Verification
In the headset: mouths move in time with every voiced line; profiler shows lip sync within budget with 2 speakers; mismatched profile → recalibrate.

## Files changed
uLipSync package entry in `unity/Packages/manifest.json`, profiles in `unity/Assets/_Project/Audio/LipSync/`, character prefabs.

## Dependencies
p0-characters-s05; p0-voice-audio-s03.

## Common issues
Lip sync drifts with streamed/long clips → feed the AudioSource that actually plays; mismatched profile → recalibrate per voice.

## Sources
Implementation Guide B-S2 Step 5, B-S4 Step 4; inventory D82 (uLipSync).
