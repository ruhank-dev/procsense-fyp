# p2-ai-extensions-s01 — On-device ASR spike (EXP-P2AI-1)

Milestone: **M13 P2 extensions**

## What
sherpa-onnx (or Unity Sentis) speech recognition running on the Quest; compare latency/WER with the server path.

## Why
Server-free voice would make voice input work with the headset alone — useful for teaching rooms without a GPU laptop.

## How to implement
1. Spike plugin; measure.
2. Check sherpa-onnx's Android/Unity integration path and model sizes first (record version); alternative: Unity Sentis with a small Whisper/ONNX model.
3. Build a minimal Quest app: push-to-talk → on-device transcript → show text; same vocabulary hint list as the server ASR.
4. Measure on Quest 3 and Quest 2: latency for 5 s clips, WER on the same recorded test set used in EXP-VOX-3, CPU/thermal impact while the simulation runs.
5. Decision: adopt as offline fallback, replace server ASR, or drop — ADR.

## Verification
EXP-P2AI-1 table (device, model, latency, WER, frame impact) + ADR in `docs/decisions/`.

## Files changed
`unity/Assets/_Project/Plugins/ASR/*` (spike branch), `docs/decisions/ADR-xxxx-on-device-asr.md`.

## Dependencies
p1-voice-input-s01.

## Common issues
On-device models compete with rendering for CPU/thermal headroom — measure during actual play, not in an empty scene.

## Sources
Inventory D70 (sherpa-onnx); Master Plan §26 (ASR on-device spike P2); Implementation Guide B-R7.
