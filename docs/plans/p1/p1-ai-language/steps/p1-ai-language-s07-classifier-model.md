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
- **Second look on a larger AI-judged set (18 October, v2 test scored once; v3 fresh hold-out):** with 1,940 training rows from 12 styles, TF-IDF macro-F1 0.816 (0.781 to 0.845), embeddings 0.792 (0.757 to 0.822), Qwen 3.5 4B 0.654 (0.618 to 0.684); TF-IDF minus embeddings 0.024 (-0.014 to 0.061), not separated; both ahead of Qwen by 0.14 to 0.16. Credited precision 0.845, 0.858 and 0.699; critical sentences credited under an ordinary act 2, 5 and 10; critical acts credited from speech 0 for all three. Recogniser copies: TF-IDF 83.8 percent, embeddings 76.7 percent, Qwen 67.9 percent. Latency on a quiet machine: TF-IDF 0.8 ms, embeddings 15.6 ms, Qwen 2.5 s. On the fresh v3 hold-out the shipped embedding head scores macro-F1 0.842 (the 17 October head 0.742) and TF-IDF 0.892. **The embeddings have shown no advantage over TF-IDF**; they stay the provisional default because the owner chose them and the gate is off, and `docs/decisions/ADR-0013-classifier-ai-judged-evaluation.md` records the comparison and what would change the choice.
- **Changes made after the v2 look, measured on v3:** the head was retrained on 1,940 rows; a critical-mass cap (`critical_mass_max` 0.02, tuned on v2 dev only, `tools/bench/tune_mass.py`) refuses ordinary credit when the head gives 2 percent or more to a critical act; the regular-expression guard grew to version 3 (`content/ai/critical_cues.json`). Effect on the v2 test post hoc: credited 346 to 266, precision 0.858 to 0.902, critical sentences under an ordinary act 5 to 0. On v3 (post hoc for the last two allergy cues): credited 137, precision 0.942, 0 under an ordinary act. Evasive critical sentences (`adversarial_critical*.json`): 8 of 71 and 7 of 48 would have been credited under an ordinary act; now 0 and 0 (the hold-out was written by the same author before the cue change, so it shows generalisation only to that author's wordings).
- **Gate:** `classifier` stays OFF: its suites read the independent set, which does not exist (DT-210). `classifier_synthetic` (v1) and `classifier_synthetic2` (v2, v3, adversarial) record the synthetic results for information; the latter's `independent_evidence` suite cannot pass by design.

- **Next (19 Oct 2026, ADR-0022):** this classifier becomes the candidate generator of the context-aware interpretation path (p1-ai-language-s10 to s14): the engine supplies the state and the eligible objectives, rules validate, the engine's evidence store decides. Two alternatives measured the same day stay out of the live path: zero-shot NLI (`docs/research/nli_zero_shot.md`) and a Qwen 4B rubric evaluator (`docs/research/rubric_evaluator.md`; 6/40 critical credited on test2, 4.1 s p95).

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

<!-- external-testing:start (generated by tools/plans/external_testing.py) -->
## External acceptance

This step has acceptance that only a person, device or account outside the engineering desk can give. The exact procedure, pass rule and evidence are in the single source of truth:

* [EX-01](../../../../testing/SANNAN_EXTERNAL_TESTING.md#ex-01): Independent evaluation set for the speech evaluator: authors, second labeller, adjudicator, then the single scored run (paused; a fresh held-out set may replace it)

<!-- external-testing:end -->
