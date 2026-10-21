# ADR-0016 — The classifier's credit gate needs two independent heads to agree, hard negatives and an assertion check

> **Superseded as architecture by [ADR-0025](ADR-0025-gemma-first-speech-evaluation.md) (21 Oct 2026).** The classifier, gate, cap, cue, assertion and interpretation machinery this record describes was retired and removed; the record and its measurements are kept as history.

**Date:** 18 October 2026 (third session) · **Status:** Accepted (engineer, on the measurements below; the classifier stays OFF) · **Related:** ADR-0005, ADR-0012, ADR-0013, p1-ai-language-s07, DT-219, EX-01

## Context
On a fresh set of 48 evasive critical sentences the production path (embedding head, critical-mass cap, regular-expression cues) credited 3 under an ordinary act ("Show me the sticker on your arm and read it aloud" as EXPLAIN_ACTION, "He sipped a coffee with milk before he came" and "Please come and see him within twenty minutes" as ANSWER_QUESTION). The cue list alone caught 6 of 48. The engine never credits a critical act from speech (`EvidenceStore.Submit` refuses it), so the harm is credit for an ordinary behaviour on a critical sentence; it is still a safety defect. Root cause: a 22-million-parameter embedding places an unusual wording next to ordinary speech (critical-act mass 0.010 to 0.020, under the 0.02 cap), the cue list only knows the wordings it was written from, one head decided alone, and a denial or hypothetical could be credited as the act it names. Details: `docs/research/classifier_benchmark/safety_gate.md`.

## Decision
1. **Credit needs two readers to agree** (`server/classify/ensemble.py`): the embedding head and the TF-IDF head must name the same ordinary act, each past its own threshold and guards; with the embedding service down nothing is credited.
2. **Hard negatives**: the 179 evasive critical sentences of the three tuning adversarial sets are training rows of both heads under their critical act (`tools/bench/build_safe_heads.py`; thresholds and the critical-mass cap are tuned on the v2 dev split only, cap grid kept tight at 0.05 or below).
3. **Assertion check** (`server/classify/assertion.py`): a denied, hypothetical or reported act, or a sentence that breaks off (ends in an ellipsis or a dash), earns no credit.
4. Every rule only removes credit. Critical acts are still never credited from speech; deterministic action-based scoring stays authoritative. The default backend is `ensemble` (`CLASSIFIER_BACKEND`); `embeddings`, `tfidf` and `llm` stay selectable.
5. **The gate stays OFF**: `classifier` still needs `classifier-bench-independent.json` (EX-01). A new informational gate `classifier_safety` records the safety figures and switches nothing on.
6. A frozen challenge set (v4, written before the gate was designed) is scored once; any further change needs a new frozen set (v5). A permanent regression suite (`tests/classifier_regression`, `tools/ci/test_classifier_regression.py`) keeps the three failures, evasive critical sentences of every kind, denials and valid positives.

## Alternatives
More cue patterns (the previous approach: version 4 has about 200; each new author's wording escapes them); a larger embedding model (not measured here, more latency for a service call); a language model as classifier or as a second guard (Qwen 4B and 9B name critical content as ordinary acts far more often than the heads and are slow, `safety_gate.md`); raising the critical-mass cap down to 0.01 only (the failures sit at 0.010 to 0.020, so it also removes valid credit and still has no second opinion); TF-IDF alone (safest, but credits only about 17 to 19 percent of ordinary rows).

## Consequences
On the synthetic sets: false credit 3/48 to 0/48 on the failing set and 5/60 to 0/60 on the frozen challenge set; credited precision on the v3 hold-out 0.942 to 0.987, on the v2 test split 0.902 to 0.980 and on v4 0.853 to 0.962; the price is fewer ordinary rows credited (v3 hold-out 60.9 to 33.8 percent, v2 test 59.1 to 33.1, challenge set 89.5 to 68.4 percent). Credit recall is not a goal: credit is only evidence for the debrief. None of this is independent validation (the authors, judges and builder are one model family); the owner's rule on synthetic data stands.

## Revisit if
The independent set exists (EX-01): re-run the benchmark on it and revisit the gate; a new critical wording is credited in the field or on a new frozen set; the embedding model is replaced (the heads must be rebuilt); a trained semantic second guard (entailment) is measured to beat the TF-IDF head as second reader.

## Addendum, 20 October 2026
The hybrid head's critical-mass cap, chosen here as 0.03, was revised to 0.05 (`content/assessment/act_head_hybrid.json`, block `critical_mass_tuning.revision`; evidence in `docs/research/recall_diagnosis.md`); a rebuild keeps the revision (`tools/bench/recall_cap_revision.json`).
