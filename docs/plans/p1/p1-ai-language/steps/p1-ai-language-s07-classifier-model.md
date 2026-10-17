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

## Status (17 October 2026)

- **Built:** `server/classify/embedding.py` (`EmbeddingActClassifier`: `all-minilm:22m` through Ollama, a logistic regression head in plain JSON `content/assessment/act_head.json`, per-act credit thresholds tuned on dev only, critical acts and NONE never credited, a down embedder credits nothing), `server/classify/guard.py` (a sentence that touches allergy, identity, consent, fasting, a worsening sign or a structured handover is never credited under a non-critical act) and the route `POST /classify/organisations/{id}` (backend `embeddings`, or `llm` for the language-model classifier). SBAR_ELEMENT is now a critical act in the engine (commit 7de6281).
- **Why this classifier:** `docs/research/classifier_benchmark` compares TF-IDF + logistic regression, embeddings + logistic regression and the Qwen 3.5 4B classifier on the same splits.
- **Measured on the synthetic set** (model-written, model-labelled, eight styles held out by profile, test scored once; NOT independent): embeddings macro-F1 0.809 (0.742 to 0.858), credited 83 at precision 0.916, critical acts credited from speech 0; TF-IDF macro-F1 0.78 (0.709 to 0.826), credited 68 at precision 0.882, critical acts credited from speech 0; Qwen macro-F1 0.654 (0.575 to 0.71), credited 97 at precision 0.68, critical acts credited from speech 0. Paired difference TF-IDF minus embeddings -0.029 (interval -0.097 to 0.042). The guard removed 1 credits from the embeddings classifier (0 of them correct). The language model credited a critical sentence under another act 18 times before the guard and 7 after.
- **Gate:** `classifier` stays OFF: its suites read the independent set, which does not exist (DT-210); `classifier_synthetic` records the synthetic result for information.

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
