# ADR-0014 — On-device speech: Whisper tiny.en is the candidate to try on the Quest; the server recogniser stays the default until the headset measures it

**Date:** 18 October 2026 · **Status:** Proposed (the headset measurement is Sannan's, DT-205) · **Related:** p2-ai-extensions-s01, p1-voice-input-s01

## Context
The spike asks whether speech recognition can run on the Quest itself. Only a PC-side proxy could be measured by a session without the headset: the same 40 rendered clips as the server recogniser's figure (`reports/ai/asr-wer.json`, faster-whisper small.en, WER 0.0932, accepted gate 0.25), the same normalisation and error-rate code (`tools/ai/asr_ondevice_proxy.py`, results in `reports/ai/asr-ondevice-proxy.json`). The clips are one clean synthetic voice, so these are upper bounds.

| Candidate (sherpa-onnx) | Size on disk | WER on the 40 clips | Real-time factor on this PC (2 threads, shared processor) |
|---|---|---|---|
| Whisper tiny.en, int8 | 118 MB archive | 0.2034 | 0.135 |
| Streaming Zipformer 20M, int8 (mobile) | 107 MB archive | 0.4915 | 0.040 |
| Server: faster-whisper small.en, int8 | n/a | 0.0932 | n/a (9.3 s per clip on the processor, a different measure) |

## Decision
1. The server recogniser stays the default. On-device recognition is a fallback to be adopted only if the headset measurement passes the same gate (WER under 0.25 on the Quest recording set, latency for a 5 s clip, thermal and frame-time cost while the simulation runs).
2. The candidate to build into the Quest spike is Whisper tiny.en int8: it is the only one of the two under the gate on the proxy. The Zipformer is much faster but fails the gate (it misreads one-word answers such as "None" and spoken dates).
3. Nothing about the Quest is claimed: no ARM build was made and no latency or thermal figure exists. The test cases and the result form are in `docs/plans/sannan-device-checks.md` (check DC-04).

## Alternatives
Adopt the Zipformer for speed (rejected: WER 0.49 on short answers); drop on-device speech (not justified before the headset number); Unity Sentis with an ONNX Whisper (not tried; sherpa-onnx has an Android build path to check first).

## Consequences
The Quest spike has a model, a pass line and a procedure. If tiny.en fails on the headset the fallback is the laptop path already built.

## Revisit if
The Quest measurement exists; or a licence check of the model files finds a term that blocks shipping them (the code is Apache-2.0; the model licences were not read in this pass).
