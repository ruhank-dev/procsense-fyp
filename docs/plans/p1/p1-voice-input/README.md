# p1-voice-input

**Tier:** P1 · **Builds on:** p1-backend, p0-voice-audio · **Feeds:** p1-ai-language (speech → text), scoring (communication evidence)

## Goal
The trainee can speak: push-to-talk speech recognition, voice orders with read-back, live TTS for AI dialogue — menu always remains as fallback.

## Scope
- **In:** ASR service on the server, Quest voice client (push-to-talk, subtitles), voice-order parser + read-back flow, live TTS, speech datasets for evaluation.
- **Out:** on-device ASR, Urdu (P2).

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| ASR | faster-whisper on the laptop GPU | On-device (sherpa-onnx); cloud | Accuracy + offline LAN | EXP-VOX-3 latency too high |
| Live TTS | Kokoro or Piper (from EXP-VOX-1) | Cloud TTS | Offline | — |
| Input | Push-to-talk | Always listening | Fewer errors, privacy | — |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-VOX-3 | Whisper size vs latency vs WER (accented English) | PriMock57 / Common Voice / consented team recordings | Targets met |
| EXP-VOX-4 | Voice-order accuracy | Parse recorded orders | ≥ 95% (original target) |

## Exit
Voice paths meet targets; menu fallback always works.

## Sources
Implementation Guide B-R7, B-S4, B-M8; inventory D61–D63, D66, D69.
