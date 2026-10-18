# p1-voice-input-s01 — Speech recognition service (EXP-VOX-3)

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
faster-whisper (`large-v3-turbo`, int8) on the GTX 1660 Super desktop (planning update 12 Oct 2026: also benchmarked on the RTX 4050 laptop and on CPU in `p1-ai-infrastructure-s02`): `/asr` accepts push-to-talk clips ≤ 15 s (16 kHz mono), returns text + word timings + avg_logprob; medical vocabulary initial prompt built from content (tests, equipment, drug classes, roles; ≤ 224 tokens); VAD on, beam 3, temperature 0; low confidence → 'Say again'.

## Why
Voice lets trainees practise the real communication of the job (asking, ordering, handover) — but only if recognition is accurate on accented English and medical terms.

## How to implement
1. `server/asr/service.py` (model loaded once); `tools/content/build_vocab.py`.
2. EXP-VOX-3: WER on PriMock57 sample + consented team recordings; order accuracy after read-back.
3. Reject clips > 15 s; convert to 16 kHz mono server-side.
4. Concurrency: queue requests (T-ASR6), never crash.

## Status (17 October 2026)

- **Built:** `server/asr/service.py` (faster-whisper loaded once, one clip at a time, clips over 15 s and non-audio refused, 16 kHz mono conversion by ffmpeg, vocabulary prompt from `tools/content/build_vocab.py`, beam 3, temperature 0, voice-activity filter, low-confidence flag) and `POST /asr/organisations/{id}/transcribe` (audited, rate limited, optional dependency `uv sync --extra asr`; without it the fallback signal). 9 tests with a fake recogniser.
- **Measured** (`tools/ai/asr_wer.py`, small.en on cpu, 40 rendered voice lines, spoken and written forms of times and dates counted equal): word error rate 0.0932 on clean synthetic speech from one voice; 9.29 s a clip on the CPU. Not a measure of accented trainees (DT-205).

## Status (18 October 2026, second session)

- **T-ASR1 met on the card.** The recogniser runs on the GTX 1660 SUPER beside the resident 4B model (about 5.3 GB of the 6 GB at the peak): `ASR_DEVICE=cuda ASR_COMPUTE=int8_float16` with the `asr-gpu` extra. Median 0.58 s for the 3 to 7 s synthetic clips (0.7 s target; 5.9 s on the processor), word error rate 0.0847 (0.0932 on the processor).
- **Real speech (EXP-VOX-3, the public part):** PriMock57 (57 mock consultations by clinicians and actors, CC BY 4.0; 5 consultations, both channels, 257 utterances of 2 to 15 s): word error rate **0.217** (doctors 0.234, patients 0.183), against the 0.25 line; median 0.699 s for 91 utterances of 4 to 6 s (a pass by a millisecond), 95th percentile 1.02 s. `tools/ai/asr_primock.py`.
- **Not done:** consented recordings of the team and of accented trainees in a noisy room (DT-205). A fix found on the way: PyAV 17 or later breaks faster-whisper 1.2 (`metadata_errors`), so the `asr` extras cap it.

## Verification
T-ASR1 5 s clip ≤ 0.7 s target; T-ASR2…T-ASR6 pass; thresholds documented.

## Files changed
`server/asr/service.py`, `server/routers/asr.py`, `server/schemas/asr.py`, `tools/content/build_vocab.py`, `tools/ai_eval/asr_wer.py`, `thresholds.yaml` (asr.min_avg_logprob).

## Dependencies
p1-backend-s06.

## Common issues
CUDA not found → matching cuDNN/CTranslate2 build; slow on 1660 → beam 1 for short clips.

## Sources
Implementation Guide B-R7, T-ASR1–T-ASR6; inventory D61–D63, D69.
