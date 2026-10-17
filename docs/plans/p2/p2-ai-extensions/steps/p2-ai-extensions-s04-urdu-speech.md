# p2-ai-extensions-s04 — Urdu / Urdu-English speech

Milestone: **M13 P2 extensions**

## What
ASR + TTS support for Urdu and code-switched speech via TTS augmentation + small consented recordings (no suitable free dataset known).

## Why
Many trainees and patients in Pakistan code-switch between Urdu and English; voice input that only understands English excludes natural speech.

## How to implement
1. Consented data collection; WER evaluation.
2. Scope: Urdu and Urdu-English code-switching for patient questions/answers (trainee speech first); no translation of clinical content without review.
3. Data: consented recordings from volunteers (consent form, speaker IDs, no identifiers), plus TTS-augmented Urdu prompts; dataset card.
4. Evaluate Whisper multilingual on the set (WER per language mix); vocabulary prompt with transliterated medical terms.
5. TTS: check available Urdu voices for the offline pipeline; record terms.

## Status (17 October 2026)

- **Not started:** Urdu speech needs consented Urdu recordings and a recogniser benchmark on them (DT-212, DT-210).

## Verification
WER by category recorded; dataset card complete; ADR on whether Urdu input ships.

## Files changed
`data/urdu/*` (dev-only, consented), `tools/voice/urdu_eval.py`, dataset card, `docs/decisions/ADR-xxxx-urdu.md`.

## Dependencies
p1-voice-input-s01.

## Common issues
Code-switched speech confuses language detection → force multilingual decoding and evaluate per mix category.

## Sources
Inventory D62 (Common Voice), D64 (Urdu/Urdu-English — no suitable free dataset); Implementation Guide B-R7 T-ASR5.
