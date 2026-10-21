# Towards the 90 percent target: baseline attribution, the safe context fix, the label conflict, the isolated critical-claim verifier (p1-ai-language-s19, 21 October 2026)

> **Historical (21 Oct 2026):** this page describes the classifier, cap and gate architecture that [ADR-0025](../decisions/ADR-0025-gemma-first-speech-evaluation.md) retired. Its findings are kept as the record of why; the current design and its results are in `docs/research/gemma_evaluator_results.md`.

**Target (owner, 21 Oct 2026, replaces the earlier 85 percent):** legitimate recall at least 90 percent (126/140 on the 140-row benchmark), credited precision at least 95 percent, 0 observed critical false credits, 0 observed adversarial false credits, strict schema compliance at least 99.5 percent, critical actions never credited from speech alone. The target is not lowered and no guard was weakened to approach it. All numbers are synthetic and builder-written; dev, test and test2 have already influenced the design, so nothing here is a validation of the target. Final acceptance needs new independently labelled data (EX-01), then real audio and the headset. `speech_interpretation` and `interpret_llm_enabled` are OFF; `cap_policy` is `hard`; the cue, the thresholds and the engine are unchanged; the frozen safety final set was not read.

## 1. Valid baseline
Live `gemma4:31b` replies, hard cap, dev: **92/140**, credited precision 92/93, critical 0/40, adversarial 0/300, out-of-window 0. The shipped path alone credits 95/140 (95/96). The corroborated cap (113/140 on replay) is research-only: it failed the bounded live panel and is not an acceptable configuration.

## 2. Attribution of the remaining dev misses
Under `hard` (48 rows not credited; first blocking component) and under the research-only corroborated policy (27 rows):

| primary cause | hard | corroborated | what the rows are |
|---|---|---|---|
| critical-mass cap (classifier/head disagreement with the model's reading) | 31 | 9 | the head's critical classes hold more mass than the ordinary act; 21 of the 31 have the ordinary act outweighing the mass (the recoverable family), 10 do not (short clarifications, speech-recognition typos such as "is that clearso far", "nrvos", "blud", "mnitor") |
| engine threshold on the head probability | 6 | 6 | the model is right for 3 (EXPLAIN_PROCEDURE "A small tube goes into your arm first" at 0.55 against 0.592; ACKNOWLEDGE_FACT with a typo; REPORT_FAULT 0.54 against 0.747); in 3 the model chooses a different, label-contested act (EXPLAIN_ACTION against EXPLAIN_PROCEDURE; REASSURE against ACKNOWLEDGE_EMOTION) |
| critical cue or critical-act menu (shipped, before the model) | 5 | 5 | the "handover" cue on "I would like the senior doctor to see him" and "I would like a chest x ray requested" (and their recognition-error copies) and one head top act that is critical |
| scenario/window or missing context | 3 | 3 | **model had no knowledge that a patient question was pending**; fixed in section 3 |
| timeout | 2 | 2 | from the live run before the per-attempt timeout fix; re-run in section 3 |
| wrong act offered (contested label) | 1 | 2 | EXPLAIN_ACTION against EXPLAIN_PROCEDURE |

No row of the 140 is lost to schema/format failure, assertion, negation or actor confusion, speech-act confusion or an objective-specific rubric deficiency in this live run: the semantic reader's own errors are small (the model's reading was right in all but the label-contested rows). The recall ceiling is set by the downstream independent checks. Note the baseline: **the reader does not beat the shipped path on dev** (both 95 after the context fix; 92 rows in common, shipped-only d0097, d0238, d0252, reader-only d0066, d0303, d0408).

## 3. Safe context fix and the timeout re-check (implemented)
The engine's own `patient_question_pending` and `readback_pending` are now stated to the reader as "Engine state (not from the speech)" lines (`server/interpret/llm/prompt.py`); they are emitted only when the engine holds them, change no eligibility (the validation, the cap, the threshold and the engine store are untouched) and cannot be forged by the speech (tests). One bounded live re-check (`tools/bench/context_recheck.py`, `reports/ai/context-recheck.json`; gemma4:31b free plan; 25 calls, 34,321 tokens; median 686 ms, max 1,816 ms) of the 24 rows whose prompt can change or that timed out:

| | before | after |
|---|---|---|
| rows credited under the right act (24) | 12 | **15** |
| ANSWER_QUESTION rows recovered | | AB-d0066, AB-d0079, AB-d0082 (the model now reads the reply as ANSWER_QUESTION) |
| wrong act offered | 0 | 0 |
| the 4 historical timeouts | 4 timeouts | 4 valid first-attempt replies (the per-attempt timeout fix); none recovered a credit because the cap holds them (AB-d0190, AB-d0440) or they were already credited |
| schema | | 24 of 24 rows ended valid; 1 row needed one format retry (24 of 25 calls valid on the first attempt = 96 percent; final compliance 100 percent) |

Estimated dev recall under the hard cap with the fix: **95/140** (92 + 3), precision 95/96. The CLOSED_LOOP rows are all held by the cap, not by the context, so the readback flag recovered none. Every other row's prompt is byte-identical to the live run (temperature 0, fixed seed), so its recorded reply stands.

## 4. Deterministic ASR/typo normalisation: explored and rejected
A conservative normaliser (split two glued vocabulary words; replace an unknown token by its unique edit-distance-1 vocabulary neighbour; protect negation, modal and actor words) was run offline over the 1,464 benchmark sentences (artifacts/analysis, not committed). Of 128 changed sentences many were wrong in meaning or grammar: "promised" to "promise" (tense, modality), "thoughout" to "though out", "scarring" to "scar ring", "Raising the head of the bed" to "rising", "bleeped" to "bleed", "details" to "detail", "Can you swallow comfortably" to "comfortable". Without a full English dictionary an edit-distance rule cannot guarantee that negation, tense, actor or critical terms are preserved, and the benchmark's synthetic typos (character swaps) are not representative of real recognition errors. **Not implemented**; a safe normaliser would need a dictionary of valid words so that only non-words are touched, and real ASR output to test on (EX-01, DC-08).

## 5. The label conflict across dev, test and test2 (no change to the cue)
Rows have no dialogue, recipient or scenario metadata; the approved distinctions are the act definitions and the behaviour windows of `content/assessment/mapping.json`:

| sentence (split, label) | explained by an approved distinction? |
|---|---|
| "I would like the senior doctor to see him." (dev, ESCALATE_SPEECH, legitimate) and "I need the registrar to come and see her now." (test2, ESCALATE_SPEECH) | ESCALATE_SPEECH: "asks a senior or another team member for help or a decision"; a named senior is the recipient |
| "My recommendation is that someone reviews him now." (dev, SBAR_ELEMENT, critical), "What I would like is for him to be reviewed urgently." (dev), "My request is that someone sees her within the hour." and "I'd like her to be reviewed within the hour." (test2, SBAR_ELEMENT, critical) | SBAR_ELEMENT: "states an element of a structured handover (... recommendation)"; no named recipient, a statement of what should be done |
| "I would like a chest x ray requested." (dev), "I would like a full blood count and CRP." and "I would like the porter to take her to the scanner." (test), "I would like an abdominal ultrasound arranged." (test2): VOICE_ORDER, legitimate | VOICE_ORDER: "gives an order to a colleague" |

Partly explained: the behaviour windows differ (ESCALATE_SPEECH steps 3, 4, 6, 7, 8, 11, 12; VOICE_ORDER 8, 10, 12; SBAR_ELEMENT 12, 15) and a named recipient separates the escalation pairs, but the cue and the head ignore the running steps and the recipient, and "someone sees her" and "the senior doctor to see him" are both requests for a person to attend: **no approved distinction separates the two escalation sentence types reliably, so they are recorded as label conflicts requiring EX-01 independent review**; the cue was not tuned. The isolated verifier (section 6) also cannot separate them (it flags both).

## 6. The isolated critical-claim verifier (`server/interpret/llm/verifier.py`; experiment `tools/bench/verifier_experiment.py`; not integrated, no cap lifted)
Question to the model: is the utterance ABOUT any approved critical topic (the five critical acts with their approved definitions), whatever the speaker does with it; input only the utterance and the engine's running steps and eligible critical acts, never the reader's reading; strict schema `critical-claim-1`, nothing repaired, an unusable answer is "not cleared". ONE bounded run (`gemma4:31b`, free plan, 77 calls, 43,261 tokens of 95,000, all 77 replies valid on the first attempt, median 651 ms, max 5,690 ms). The panel was fixed before the run (`tools/bench/verifier_panel.json`): the 12 open panel sentences, the 31 cap-blocked legitimate dev rows, the 6 surviving false-credit rows, 20 dev critical rows, 8 wording-conflict rows. **The verifier prompt was written after the panel failures were known, so the open-panel result is not held out.**

| | critical content (expected) | legitimate or ordinary (expected) |
|---|---|---|
| flagged as critical | **detected 34** | flagged 7 (conservative) |
| cleared | **missed 0** | cleared 35 |

Sensitivity 34/34 (95 percent Wilson lower bound 0.90); specificity 35/42 = 0.83 (lower bound 0.69); one unlabelled sentence flagged. Per group: open panel 9/9 critical detected and 2/2 ordinary cleared (the 5 sentences the reader had OFFERED are all flagged); surviving false-credit rows 5/5 critical detected, 1/1 cleared; dev critical sample 20/20; cap-blocked legitimate 27/31 cleared; wording-conflict legitimate 5/8 cleared (the three flagged are the escalation requests, labelled SBAR_ELEMENT by the verifier). A critical miss would be far more serious than a conservative flag; there was none, on 34 critical sentences from a design-informed set.

**What it would mean, if it were used to gate the cap (NOT done, estimates from recorded data):**
* of the 31 cap-blocked legitimate rows, the policy of the corroborated cap recovers 21; the verifier clears 19 of those 21 (AB-d0376 and AB-d0394 are flagged) and 27 of all 31;
* estimated dev recall under the hard cap with the context fix and a verifier-gated lift: **95 + 19 = 114/140 (81.4 percent)**, credited precision about 114/116, critical and adversarial offers 0 in the replay (the gated rule is a subset of the corroborated rule, so it cannot offer more);
* the 67 historical raw false credits: the corroborated rule offers 0 of 67 in every view, so a rule that adds a verifier condition offers 0 of 67; the 6 surviving patterns: 0 by the corroborated rule, and the verifier flags 5 of 5 critical ones;
* the 5 panel sentences that failed: all 5 flagged, 0 would remain offered;
* the contradictory handover examples: not distinguishable (section 5).
Recall would remain below 90 percent: the remaining misses are the 10 cap rows without dominance, the thresholds (6), the critical cue (5), the contested labels (2) and the 2 flagged rows.

Limits: one model family judges both the reading and the verifier (not fully independent), the sample is small, the verifier prompt was informed by the failures, and 77 sentences are builder-written. The result is a reason to run a larger, held-out test (EX-01), not evidence that the cap can be lifted.

## 7. Evolution table (dev, 140 legitimate rows; live gemma4:31b replies unless stated)
| configuration | recall | credited precision | critical / adversarial / out-of-window offers | schema | latency |
|---|---|---|---|---|---|
| shipped path (no model) | 95/140 (67.9%) | 95/96 | 0 / 0 / 0 | n/a | local, no model call |
| semantic reader, hard cap (s18 live run) | 92/140 (65.7%) | 92/93 | 0 / 0 / 0 | 137/137 replies valid, 137/141 requests (4 timeouts, fixed) | median 895 ms, p95 1,302 ms |
| + stage one (critical objective under any verdict is the menu), still hard | 92/140 | 92/93 | 0 / 0 / 0 | unchanged | unchanged |
| corroborated cap (research only, FAILED the live panel: 5 of 12 critical-content sentences offered) | 113/140 replay | 113/115 | 0 / 0 / 0 on the benchmark; 5 critical-content panel sentences offered | unchanged | unchanged |
| **+ engine-state context, hard cap (current best valid configuration)** | **95/140 (67.9%)** estimated from a 24-row live re-check | 95/96 | 0 / 0 / 0 | 24/24 final valid, 24/25 first attempt (96%) | median 686 ms, max 1,816 ms |
| hypothetical: + verifier-gated cap lift (NOT implemented, needs owner approval and held-out data) | 114/140 (81.4%) estimated | about 114/116 | 0 / 0 / 0 estimated; the 5 failed panel sentences all flagged | 77/77 verifier replies valid | median 651 ms extra call |
| target | >= 126/140 (90%) | >= 95% | 0 / 0 | >= 99.5% | |

**Does 90 percent look achievable without weakening safety? Not on these datasets and not with the present independent checks.** The reader recovers little because its mistakes are small; the ceiling is set by independent barriers: the cap (31 rows, of which a verifier-gated lift might safely recover about 19), the engine thresholds (6), the critical cue (5, a label conflict) and contested labels (about 4). Even with the most favourable assumption (the verifier lift works, the thresholds follow the model where it is right, the cue cleared) the sum is about 114 + 6 + 5 = 125, and each of those three needs an approval or independent data that is not available. 126/140 cannot be reached or shown with this synthetic data without overfitting. The honest next step is independent labelled data (EX-01), measuring the verifier-gated rule on it, then real audio.

## 8. Classification of what remains
1. **Safe enough to implement now (done):** engine-state context lines; stage one (critical objective under any verdict is the menu); the isolated verifier module and its experiment tool (not wired in).
2. **Experimental ideas that need your approval:** a verifier-gated lift of the cap behind a new setting (needs a held-out test first); a head-probability rule for an agreeing model on a different act (`engine_floor`, DT-248) for the 3 rows where the model is right and the head is just below the threshold; an extra live run for a larger verifier panel.
3. **Dataset and label issues for EX-01:** the escalation/recommendation wording conflict (dev, test, test2); EXPLAIN_ACTION against EXPLAIN_PROCEDURE; ACKNOWLEDGE_EMOTION against REASSURE; whether "I would like a chest x ray requested" is VOICE_ORDER or SBAR.
4. **Cannot be improved safely without changing an existing guard:** the 10 cap rows without dominance (typos and short clarifications), the 5 cue rows, the 6 threshold rows, unless a guard is redesigned with your approval; deterministic ASR normalisation without a dictionary.
