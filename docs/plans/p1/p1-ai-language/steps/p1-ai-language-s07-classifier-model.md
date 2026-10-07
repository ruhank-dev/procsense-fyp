# p1-ai-language-s07 — Communication classifier (EXP-AI-3)

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
`/classify` → acts with topic, span and confidence (+ SBAR element, flags like jargon); per-act thresholds calibrated on dev for precision ≥ 0.8; critical acts recorded but never credited from speech; async (evidence may arrive later).

## Why
Communication (explaining, inviting questions, SBAR) is half of the walkthrough's competencies; a classifier lets spoken communication earn credit — but only for non-critical items, and only above a calibrated threshold.

## How to implement
1. Prompt generated from the taxonomy; temperature 0.
2. `tools/ai_eval/calibrate.py`; weekly F1 + confusion matrix.
3. Evidence arrives asynchronously; the scorer accepts it until case end (B-M3).
4. Weekly report: per-act F1, confusion matrix, threshold table.

## Verification
T-CL1 macro-F1 ≥ 0.75 on the frozen test split (else debrief-only mode); T-CL2 critical acts never credited.

## Files changed
`server/routers/classify.py`, `server/schemas/classify.py`, `prompts/classify/v1.txt` (generated), `tools/ai_eval/calibrate.py`, `thresholds.yaml`.

## Dependencies
p1-ai-language-s06.

## Common issues
Over-labelling REASSURE → raise its threshold and add negative examples; low SBAR recall → separate SBAR prompt.

## Sources
Implementation Guide B-R8 Steps 1–5, T-CL1–T-CL5; B-M3 (critical guard).
