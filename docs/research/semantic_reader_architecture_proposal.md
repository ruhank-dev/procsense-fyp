# Proposal: independent safety verification between the semantic reader and the scoring (DT-238), from the failure attribution of 21 October 2026

> **Historical (21 Oct 2026):** this page describes the classifier, cap and gate architecture that [ADR-0025](../decisions/ADR-0025-gemma-first-speech-evaluation.md) retired. Its findings are kept as the record of why; the current design and its results are in `docs/research/gemma_evaluator_results.md`.

**Status (updated 21 Oct 2026): the proposal was approved and implemented behind `interpret_llm_cap_policy` (default `hard`); the bounded live panel check FAILED for the corroborated policy (5 of 12 open sentences offered), so it is OFF and must not be enabled: see `docs/research/corroborated_cap_results.md`. The text below is the proposal as written before the implementation, including its estimates, which the replay confirmed (113/110/95/104) and the live panel contradicted for safety.** Evidence: `docs/research/failure_attribution.md` (generated; numbers below are read from it and from `reports/ai/failure-attribution-dev.json`). All rows are synthetic and builder-written; the dev split is where the baseline and prompts were developed; no cloud call was made for this analysis; no guard, threshold, flag or frozen-set content was touched. `speech_interpretation` and `interpret_llm_enabled` stay OFF.

## 1. What the audit found (short)
* **118 against 92 is a net figure.** The earlier guarded evaluator credited 118 of the 140 legitimate dev rows; the live production pipeline credits 92 (the replay reproduces it). Row by row: both 84; **A only 34; B only 8**. The earlier evaluator's rule had no head, no cap, no window, no engine threshold, no waiting-state rule: it was a measurement of what the model *reads*, not of what the system *accepts*.
* **Where the 34 were lost:** 27 by the head's critical-mass cap (0.05), 4 by the engine threshold applied to the head's probability, 2 by a model timeout that fell back to the shipped refusal (which is the cap), 1 by the critical-act menu. In 30 of the 34 the live reading supported the row's own act: **the model was right and a later component refused it.** The 8 rows B gained: 3 ANSWER_QUESTION rows (the earlier evaluator's model named EXPLAIN_PROCEDURE), 3 rows whose earlier recorded reply was malformed (fenced), 1 where the earlier reply named a critical objective (SBAR_ELEMENT, never credited) and 1 where it credited nothing.
* **The cap is the dominant loss**: of all 48 rows B misses, 31 are the cap's first refusal (34 have a mass over 0.05); for 28 of the 34 the head's top act is the row's act and for 28 the act outweighs the whole critical mass. The mass comes from CONSENT_SIGN, SBAR_ELEMENT, STATE_ALLERGY, HANDOVER_CRITICAL_ELEMENT and CONFIRM_IDENTITY in turn ("Do you get any cough?" has mass 0.44 on STATE_ALLERGY). No critical cue matched any of them.
* **The false credits of the raw Gemma runs** (67 unique non-legitimate rows over dev, test, test2; strict or fenced views): the assertion layer and the cue removed 61; **6 survived the earlier guards, all on test2**: 3 allergy-like questions (a food question, its recognition-error copy, "has a plaster ever left skin red and sore") read as ASK_TOPIC, 2 escalation or review requests read as ESCALATE_SPEECH or VOICE_ORDER, and one negation ("I never got to say...") read as REASSURE. Every one has a critical mass of 0.105 or more (0.33 to 0.97 for the five critical ones) and in the five critical ones the ordinary act is *outweighed* by the critical classes (p(act) 0.003 to 0.35 against 0.33 to 0.97).
* **The current pipeline's one wrong offer (92/93)** is a legitimate sentence under a contested act (EXPLAIN_ACTION against EXPLAIN_PROCEDURE: the head and the model agree); it is a label question for EX-01, not an unsafe credit.

## 2. The independent signal that exists today and is not used
The model's own reading names a critical objective for **39/40, 39/40 and 35/40** critical rows on dev, test and test2 but for **1/140, 0/140 and 2/140** legitimate rows (fenced view so that no reply is lost). The pipeline uses this reading only when the objective is SUPPORTED. The five test2 critical rows with no critical finding are exactly the ones the model missed and exactly the ones the cap and the dominance condition catch. So the model's reading and the head's mass fail on different rows: they are **independent enough to corroborate each other.**

## 3. The target separation

```
speech + engine context
   -> 1. SEMANTIC INTERPRETATION   (the LLM, broad natural language; output: candidate objective, evidence span, AND an explicit statement of which critical topics the utterance touches)
   -> 2. INDEPENDENT SAFETY / CLAIM VERIFICATION   (several detectors with different failure modes; only ever route to the menu or abstain; none can add credit)
        a. lexical: the cue regexes and the allergen lexicon (shipped)
        b. statistical: the head's critical mass and its dominance over the candidate act (shipped cap, dominance new)
        c. linguistic status: the assertion layer (shipped)
        d. model-independent of the candidate: the reading's critical-topic report (new use of an existing field)
        e. scenario state: behaviour windows, waiting question and read-back, behaviours already credited (shipped)
   -> 3. AUTHORITATIVE DETERMINISTIC SCORING   (the engine's evidence store; critical physical actions only from verified Unity or state evidence; unchanged)
```
The LLM never becomes the authority: stage 2 can remove an offer but never create one; stage 3 is the only place credit exists; a critical objective is never credited from speech at any stage (ADR-0005).

## 4. Options

| option | change | evidence from the recorded data | still unproven |
|---|---|---|---|
| 0 keep | none | live dev 92/140, shipped 95/140 | recall ceiling set by the cap |
| **1 (smallest)** | **corroborated cap**: (i) a reading that reports ANY critical objective sends the sentence to the menu (a strengthening); (ii) the cap is lifted for a candidate only when the reading is strict-valid, names exactly one non-critical SUPPORTED objective, reports no critical objective, and the head's probability for that objective is at least the whole critical mass; every other check unchanged | table below | the 8 panel sentences (below); fresh data; the live context prompt |
| 2 | a separate narrow critical-claim call (a second model question: "does this touch identity, allergy, consent, fasting, worsening, handover?") | not measurable from recordings | needs a live experiment; adds latency and cost |
| 3 | scenario-state evidence: while a critical check is pending in the running steps, speech touching that topic is the menu | needs an engine context field | a design decision on the context contract |
| 4 | an independent critical-content classifier trained on independent data | needs EX-01 data | long term |

## 5. Estimate for Option 1 from the existing recordings (counterfactual replay, nothing implemented)
The variant V2 of the attribution page is Option 1 (ii); the pipeline, the engine's evidence store and every other check run as shipped.

| replies | current pipeline | with Option 1 | shipped path alone | critical false credit | adversarial false credit | credited precision |
|---|---|---|---|---|---|---|
| dev, live replies | 92/140 | **113/140** (+21, none of the current 92 lost) | 95/140 | 0/40 | 0/300 | 113/115 |
| dev, recorded strict | 88/140 | 110/140 | 95/140 | 0/40 | 0/300 | 110/112 |
| test, recorded strict | 75/140 | 95/140 | 76/140 | 0/40 | 0/300 | 95/95 |
| test2, recorded strict | 75/140 | 104/140 | 79/140 | 0/40 | 0/300 | 104/104 |

* **How many of the 140 it could recover:** about 21 on dev with the live replies (to 113, 80.7 percent), about 20 on test and 29 on test2 on the recorded replies. Without the dominance condition (V1) test2 lets one critical sentence through (a recognition-error copy of the food question): the dominance condition is what removes it.
* **What it still blocks:** all 67 non-legitimate rows that the raw replies credited. The 56 non-performed ones (negation, incomplete, another speaker, report, plan, hypothetical) by the assertion layer, unchanged; the 5 cue matches by the cue; the 6 guarded survivors by the cap (not lifted: dominance fails for 5, the engine threshold for the sixth) and the engine threshold. No safeguard is removed; (i) adds a block.
* **What remains of the 140 on dev (live replies):** 27 rows are still not credited. Of the 12 that had a cap refusal in the current pipeline, 5 are still refused at the cap (the reading or the dominance condition does not allow the lift), 2 are model timeouts, 1 is offered under the contested act, 1 the model did not support, and 3 end at the engine threshold or the window. That is why even Option 1 does not reach 85 percent on dev with the live replies (80.7).
* **The residual risk it does not close:** the 8 sentences that leak when the cap is removed from the safety panel (fasting question, blood thinner, "initial each box", senior anaesthetist...) all have the ordinary act outweighing the critical mass (ratio 1.9 to 10). Dominance does not stop them; only the model's critical-topic report could, and the model has never been asked about them. **Option 1 must not be implemented until a small live run (about 20 sentences, the panel's cap-only sentences) shows that the model reports a critical objective for them**, and until a fresh set confirms the recall without a safety cost.
* The conditions were read off the analysis of dev, test and test2 and were not tuned on held-out data; the estimate is exploratory. Zero observed unsafe offers on synthetic rows is not a safety guarantee.

## 6. Recommendation
**The smallest safe architectural change with the strongest evidence is Option 1, as a setting that defaults to the present hard cap (`cap_policy = hard`), added in two steps: first (i) alone** (the reading's critical report becomes a block: it can only remove offers; evidence: it flags 113 of 120 critical rows and 3 of 420 legitimate rows) **and then (ii) the corroborated cap**, only after the live panel check and a fresh-data check pass. Estimated gain from (ii): about 20 to 29 legitimate rows per 140 on synthetic data, with 0 of the previously observed unsafe credit categories reopened. It does not need a new model, a new threshold or a change to the engine, and it keeps every present safeguard. It does not make the system clinically safe, does not reach the 85 percent target on its own, and does not replace the independent critical-content signal (Option 4) or independent labels (EX-01).

## 7. Decisions requested (nothing below was done)
1. Approve implementing Option 1 (i) and (ii) behind `cap_policy` (default `hard`), with unit tests, mutants and the documentation, as a new step.
2. Approve one small live run (about 20 panel sentences, free plan, `gemma4:31b`, under 50k tokens) to measure whether the model reports a critical objective for the cap-only panel sentences, before (ii) is enabled anywhere.
3. Decide whether a pending critical check in the running steps should be part of the context contract (Option 3).
4. The contested EXPLAIN_ACTION/EXPLAIN_PROCEDURE sentence and the 12 remaining capped rows are label and head questions for EX-01, not for this change.
