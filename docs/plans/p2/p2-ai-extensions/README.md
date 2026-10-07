# p2-ai-extensions

**Tier:** P2 · **Builds on:** p1-ai-language, p1-voice-input

## Goal
- **On-device speech recognition** on Quest (sherpa-onnx spike) so voice works without the laptop.
- **Retrieval/embeddings** over approved content for grounding and debrief evidence.
- **Distilled small classifier** replacing the LLM-based classifier (faster, cheaper).
- **Urdu / Urdu-English speech** support (no suitable free dataset known → TTS augmentation + consented recordings).
- **Medical LLM benchmark** (e.g. MedGemma, terms to accept) vs general model.

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| On-device ASR | sherpa-onnx spike first | Unity Sentis models | Original plan | Spike fails latency/WER |
| Urdu data | TTS augmentation + small consented set | Wait for public dataset | None suitable known (inventory D64) | Dataset found |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-P2AI-1 | On-device ASR latency/WER on Quest | Spike | Comparable to server path |
| EXP-P2AI-2 | Small classifier vs LLM classifier | Same eval set | F1 within agreed margin |
| EXP-P2AI-3 | Medical vs general LLM | EXP-AI-1 suite | Better grounding/quality → switch |

## Exit
Each extension meets its target or is documented as not viable (with evidence).

## Sources
Inventory D62, D64, D68, D70; Master Plan §26 (components 4, 5).
