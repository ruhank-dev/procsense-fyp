# synthetic_dev_v1: open issues and discoveries (tracked separately from the labelled data)

This is **DEVELOPMENT data**, AI-written and AI-reviewed (the same model), synthetic. It is not independent, not human-labelled and not real-world validation. Failures on it may be inspected and used to improve ProcSense; results on it are never generalisation evidence. A fresh held-out set (personas P5 and P7, reserved topic pool `eval`) is built later and frozen before scoring.

## Excluded from authoritative gold (not generated, or generated then dropped)
| id / area | why | tracked in |
|---|---|---|
| delegated communication ("ask the nurse to explain", "get the patient to sign") | no approved label (VOICE_ORDER vs the concerned act vs NONE) and unknown effect on b12 | DT-243 |
| claims of earlier completion ("I explained that earlier"); SD1-0470 dropped because a past-tense report of a non-clinical fault reads as that claim | not an approved non-performed kind; possible guard gap | DT-244 |
| broken-off / incomplete speech | no evidence of how real ASR renders a cut-off; the incomplete kind is a punctuation convention | DT-242 (a) |
| speech by anyone other than the trainee | not an interpreter input | ADR-0022 |

## Dropped specifications (boundaries the approved text does not decide) — candidates for a later hard-case challenge set and for definition work (DT-241)
See `discards.jsonl` for the exact reason of each. SD1-0284 (consent topic inside an ordinary escalation), SD1-0040 / SD1-0159 / SD1-0260 (fasting instruction inside an ordinary act), SD1-0208 (ACKNOWLEDGE_EMOTION vs REASSURE in one sentence), SD1-0367 (allergy documentation question), SD1-0470 (DT-244 look-alike).

## Semantic observations kept in the labelled data (flagged, not hidden)
* **Announcing another person's action** (SD1-0129, SD1-0135, "The healthcare assistant is about to put a cuff on your arm"): the speaker tells the patient what is about to happen, but does not do it. The approved assertion layer files it under other_actor (not credited); whether a trainee announcing a colleague's action should count as EXPLAIN_ACTION is not stated. Gold: not credited.
* **Performed sentences that open like a hypothetical or a plan** (the performed-versus-hypothetical tail): SD1-0254 ("I'll just read that back to be safe, potassium five point two"), SD1-0260 was dropped, SD1-0261 ("so that's the monitor on, I'll go and do that straight away"), SD1-0374 ("If you're happy, would you mind signing …", critical, never credited), SD1-0138 ("I'm just going to feel your tummy now"). They perform the act now; a reader that keys on "if" or "I'll" would misjudge them.
* **Reported speech that also informs**: reported members never state the content as the speaker's own (a reported answer would otherwise read as ANSWER_QUESTION).
* **Hedged performance**: only hedges on the content of a performed act ("it should take about an hour, I think") are used (28 specifications flagged `hedge`); a hedged act ("maybe I could explain …") is not generated.
* **ESCALATE_SPEECH vs VOICE_ORDER (rubric rule 4)**: a request to a colleague to bring equipment or fetch a person is VOICE_ORDER; a request to a senior to come, advise or decide is ESCALATE_SPEECH. SD1-0498 puts "the sister will call the engineer" under another-actor ESCALATE_SPEECH; whether an engineer is a "senior or team member" is unspecified (the member is not credited either way).
* **Question vs request wording**: polite requests are often written as questions ("Could you tell me …?"); the form field is recorded and is not label-critical for the question-ok acts.
* **Specification form defaults for no-act items**: `form` is a default for NONE items (SD1-0422, 0425, 0433, 0466, 0467 are questions to nobody in particular); not label-critical.

## Reserved for the held-out evaluation set
Personas P5 and P7 (never used here) and the reserved topic pool `eval` (this set's seeds are the pool `dev`).
