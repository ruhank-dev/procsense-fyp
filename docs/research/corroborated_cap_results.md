# The corroborated cap: implementation, replay, the bounded live panel, and what is still wrong (p1-ai-language-s19, 21 October 2026)

> **Historical (21 Oct 2026):** this page describes the classifier, cap and gate architecture that [ADR-0025](../decisions/ADR-0025-gemma-first-speech-evaluation.md) retired. Its findings are kept as the record of why; the current design and its results are in `docs/research/gemma_evaluator_results.md`.

**Status: implemented behind a setting that is OFF; the live panel experiment FAILED for the corroborated policy. Do not enable `interpret_llm_cap_policy=corroborated`.** The default is `hard` everywhere. `speech_interpretation` and `interpret_llm_enabled` stay OFF. Every sentence is synthetic and builder-written; dev, test and test2 informed the design of the policy, so the replay recovery below is exploratory and is not a generalisation estimate. No claim of clinical, audio or headset validity is made.

## 1. What was implemented
Code: `server/interpret/semantic.py` (`corroborated()`, `cap_policy`), `server/config.py` (`interpret_llm_cap_policy`, default `hard`), `server/routers/interpret.py` (wiring). Nothing in the shipped Interpreter, the assertion layer, the critical cues, the head, the thresholds, the engine or the frozen safety files changed.

| stage | rule | effect |
|---|---|---|
| 1 (always on when the reader runs, both policies) | a strict-valid reading that reports ANY critical objective in ANY finding (SUPPORTED, UNCERTAIN or NOT_SUPPORTED) sends the sentence to the menu of verified actions (`LLM_NAMED_CRITICAL`) | can only remove an offer; before s19 only a SUPPORTED critical finding did this |
| 2 (only `cap_policy=corroborated`) | the head's critical-mass cap is not applied to a model candidate only if: the reading is strict-valid, exactly one finding is SUPPORTED, no finding names a critical objective, and the head's probability for that objective is at least the whole critical mass; reason code `CAP_CORROBORATED` | can create an offer where the cap refused one; every other check still applies |

After stage 2 the candidate still passes: the shipped Interpreter's order (critical cue and critical act first, the model is not called for them), the assertion layer (non-performed sentences are not sent to the model), the behaviour window, the waiting-question and read-back rules, the one-credit-per-behaviour rule, the engine threshold on the head's probability, and the engine's evidence store (which refuses critical acts, unknown acts, duplicates and out-of-window evidence). "The head supports the same objective" is implemented as the head's probability for the objective being at least the total critical mass (the estimate's variant V2); "the head's top act is that objective" (V3) was not required.

Tests: 8 new test classes and parametrisations in `server/tests/test_semantic_interpreter.py` (the six previously surviving raw false-credit patterns under both policies, a recovered row under both policies, stage one for each verdict, every clause of the condition including its equality boundary, the engine threshold, window, waiting state, already credited, non-performed sentences, critical cue, invalid readings, every critical act, defaults and wiring). Mutants SM9 to SM18, SC2, SC3 added to `tools/ci/mutation_safety.py`, SM2 and SM5 updated; all 29 semantic mutants are killed (one candidate mutant, "the supported objective is not critical", was removed because the clause was redundant with the next one).

## 2. Replay on the recorded replies (`tools/bench/corroborated_replay.py`, `reports/ai/corroborated-cap-replay.json`)
The real `SemanticInterpreter` through the benchmark's three contexts (in window, outside the window, none) and the engine's evidence store. "Hard" includes stage one. Counts are raw.

| replies | policy | legitimate recall | credited precision | critical offers (any context) | adversarial offers (in window / any context) | out-of-window offers | no-context offers |
|---|---|---|---|---|---|---|---|
| dev, live | hard | 92/140 | 92/93 | 0/40 | 0/300, 0/300 | 0/448 | 0/488 |
| dev, live | **corroborated** | **113/140** | 113/115 | 0/40 | 0/300, 0/300 | 0/448 | 0/488 |
| dev, recorded strict | hard | 88/140 | 88/89 | 0/40 | 0/300 | 0/448 | 0/488 |
| dev, recorded strict | corroborated | 110/140 | 110/112 | 0/40 | 0/300 | 0/448 | 0/488 |
| test, recorded strict | hard | 75/140 | 75/75 | 0/40 | 0/300 | 0/448 | 0/488 |
| test, recorded strict | corroborated | 95/140 | 95/95 | 0/40 | 0/300 | 0/448 | 0/488 |
| test2, recorded strict | hard | 75/140 | 75/75 | 0/40 | 0/300 | 0/448 | 0/488 |
| test2, recorded strict | corroborated | 104/140 | 104/104 | 0/40 | 0/300 | 0/448 | 0/488 |

* Stage one cost no legitimate row: "hard" with stage one equals the pre-s19 pipeline (92, 88, 75, 75). Corroborated lost no row that "hard" credited (gained 21, 22, 20, 29; lost none).
* The two wrong dev offers (113/115) are the contested EXPLAIN_ACTION/EXPLAIN_PROCEDURE sentence and its recognition-error copy (a label question for EX-01, not an unsafe credit); 92/93 under hard is the same sentence.
* **The 85 percent target is not reached** and is not lowered: 113/140 is 80.7 percent on dev with the live replies (the design data), 95 (67.9 percent) on test, 104 (74.3 percent) on test2.
* **The 67 historical raw false credits** (`unique rows credited by the raw Gemma replies in the dev, test and test2 experiments, strict or fenced view, guarded or not`): replayed on their own raw reply (the fenced view with the code fence removed, the worst case for the policy) under hard and under corroborated: **0 of 67 offered in any of the three contexts, in all four combinations.**
* Descriptive only, no policy uses it: the recovered legitimate rows have a head probability 1.2 to 16 times (median about 6 to 7) the critical mass.

## 3. The bounded live panel experiment (`tools/bench/cap_panel_live.py`, `reports/ai/cap-panel-live.json`)
One run on `gemma4:31b` (the free plan through the signed-in local Ollama, `think` low, temperature 0): 12 model calls, 17,988 tokens (ceiling 50,000), no retry needed, no billing or quota signal, median 700 ms, maximum 2,169 ms. The panel was fixed by `--select` BEFORE any call (`tools/bench/cap_panel.json`): every sentence of the existing safety panel (1,693 distinct, the frozen safety final set was not used) that the corroborated policy would offer if the model reported no critical objective ("open", 12 sentences, of which 8 were the sentences that leaked when the cap was removed from the panel and 4 more came from a broader search with an oracle reply), plus 8 positive controls. Nothing was changed after the replies.

| open sentence (panel set; label) | model: critical objective? | model's semantic decision | decision of the pipeline (corroborated) |
|---|---|---|---|
| The reason for my call is that the family are anxious and want a senior opinion. (SBAR_ELEMENT) | no | ESCALATE_SPEECH supported | **OFFERED ESCALATE_SPEECH** |
| Please come and see him within twenty minutes. (SBAR_ELEMENT) | no | VOICE_ORDER supported | **OFFERED VOICE_ORDER** |
| Nothing to eat or drink since midnight, is that right? (HANDOVER_CRITICAL_ELEMENT) | no | ASK_TOPIC supported | **OFFERED ASK_TOPIC** |
| Did you have breakfast this morning? (HANDOVER_CRITICAL_ELEMENT) | no | ASK_TOPIC supported | **OFFERED ASK_TOPIC** |
| i need tobe sure you unerstand the risks and accept them (CONSENT_SIGN) | no | CHECK_UNDERSTANDING supported | **OFFERED CHECK_UNDERSTANDING** |
| Would you initial each box to show you have read it? (CONSENT_SIGN) | CONSENT_SIGN | (critical) | menu |
| Remember the blood thinner was stopped three days ago. (HANDOVER_CRITICAL_ELEMENT) | SBAR_ELEMENT | (critical) | menu |
| which mdiines are you not able to have (STATE_ALLERGY) | STATE_ALLERGY | (critical) | menu |
| I am not about to repeat the check, we did it earlier. (not performed) | CONFIRM_IDENTITY, NOT_SUPPORTED | none supported | menu |
| R: Don't start until the senior anaesthetist is present. (SBAR_ELEMENT) | no | VOICE_ORDER supported | abstain (cap kept: the model chose VOICE_ORDER, the head gives it 0.025 against a critical mass of 0.125, and the head's own top act ESCALATE_SPEECH has 0.767) |
| The nurse said she said anxiety was normal. (reported speech) | no | REASSURE not supported | abstain |
| Let the senior nurse say most people do well. (another speaker) | no | REASSURE not supported | abstain |

Note on the senior-anaesthetist row: the model's reading met every condition except the head's: dominance failed for the act the model chose (VOICE_ORDER 0.025 against mass 0.125), so the independent head check stopped it, not the model.

**Result: 5 of the 12 open sentences were offered under the corroborated policy, 4 of 12 were stopped by the model's critical report (stage one), 3 abstained or were not supported by the model.** Five offered sentences are critical-content sentences of the panel (SBAR_ELEMENT twice, HANDOVER_CRITICAL_ELEMENT twice, CONSENT_SIGN once) that the model read as an ordinary act with no critical objective, exactly the failure the model's critical report was supposed to cover. The recorded dev/test/test2 benchmark rows hid this because in those 120 critical rows the model's critical report was enough; these panel sentences are the ones the head's critical mass catches only weakly (mass 0.09 to 0.27) and the model misses.

* **The positive controls never reached the model**: all 8 are refused before the model is asked (critical cue or critical act of the shipped path), so the run says nothing about the model's recall on the clear cases; that is measured by the benchmark readings (the model reported a critical objective for 39/40, 39/40 and 35/40 critical rows).
* No policy, threshold or panel was changed after the replies (the instruction was not to tune during the same experiment). A larger dominance ratio would not separate the offered sentences (head probability over critical mass 8.7, 1.1, 3.7, 1.9, 1.5) from the recovered legitimate rows (1.2 to 16): that is a descriptive observation, not a proposal.

## 4. Where the pending critical state comes from, and whether it belongs in the context (decision 3; nothing was changed)
* **What it is.** Two flags and one list already exist in the engine's `InterpretationContext` (`src/ProcSense.Core/Speech/InterpretationContext.cs`): `eligible[].critical` (an act that is eligible now AND critical), `patient_question_pending` and `readback_pending`. The "pending critical check" of the proposal is the first of these: the critical behaviours whose step window contains a running step.
* **Origin.** `InterpretationContextBuilder.Build` takes `Session.RunningSteps` (`FlowSubsystem.RunningSteps()`: the workflow runs in state Active and not suspended) and maps the steps to behaviours through the behaviour windows of `content/assessment/mapping.json`, then marks acts that the score tables list as critical. `readback_pending` is the clinical flag `critical.unacknowledged` set by `ResultReviewSubsystem`; `patient_question_pending` is the consent discussion having a question waiting (DT-232).
* **Deterministic and authoritative?** Yes: all of it is computed from the session's own state by the engine, read-only, never from speech or from a model reply. Speech cannot move a workflow step; an AI act cannot satisfy a critical behaviour (the evidence store refuses critical acts and speech is "not an action of any behaviour"). A model reply is validated against this context and cannot change it.
* **What it means.** It says "a critical behaviour is currently expected", not "the trainee has done it". It does not say whether the check was verified done (that is the scoring log) and therefore cannot tell whether a given sentence is about the check or about something else.
* **Recommendation.** It does NOT need a new field: the information is already in the context the interpreter receives. Using it to route a sentence to the menu while a critical behaviour is eligible would be a Python-side behaviour change that would also send many legitimate sentences (a sleep question at the identity-check step) to the menu, and it would not distinguish the five offered panel sentences from legitimate ones by itself. That changes the runtime behaviour beyond the approved cap-policy work, so **I did not add it and ask you first** (decision A below).

## 5. Remaining failure attribution (dev, live replies)
* Under `hard` (the shipped state of the reader): 92/140 credited. The 48 not credited, by the first blocking component: critical-mass cap 31, head probability below the engine threshold 6, critical cue or critical-act menu of the shipped path 5, behaviour window or eligibility of the candidate 3, model timeout falling back to the cap 2, one wrong act offered (the contested EXPLAIN_ACTION sentence) 1.
* Under `corroborated` (not enabled): 113/140. The 27 not credited: critical-mass cap still kept (the corroboration failed) 9, engine threshold 6, critical cue or menu 5, window or eligibility 3, wrong act offered (the contested sentence and its recognition-error copy) 2, model timeout 2. The cap, the threshold and the shipped menu remain the largest blocks, and none of them is a model error: they are the architecture's independent checks.
* **Hard conclusion for the architecture:** the recall gain from lifting the cap rests on the model's critical-content report being complete, and the live panel shows it is not (5 of 12 open sentences were offered, and all 5 are critical-content sentences of the panel). The hard cap is the only independent barrier for that class of sentence today, so it cannot be relaxed on this evidence. The model reports critical content for the clear cases and misses the indirect ones (fasting, handover-style requests, escalation requests, consent understanding), which are also the sentences on which the head's mass is lowest.

## 6. Decisions requested before any further relaxation or change (none made)
A. Whether to use `eligible[].critical` while a critical behaviour is expected as an extra block (a runtime behaviour change; section 4).
B. Whether the corroborated policy should be kept in the code behind the setting as an experiment (it is OFF and `hard` is the default) or removed.
C. Whether to build the independent critical-content signal (DT-238 option 4) on independent data (EX-01) before any lift of the cap is reconsidered.
D. The contested EXPLAIN_ACTION/EXPLAIN_PROCEDURE label (EX-01).
