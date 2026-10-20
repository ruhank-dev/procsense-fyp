# Audit of the speech-safety layers (20 October 2026, p1-ai-language-s16, ADR-0023)

Starting points: `docs/research/recall_diagnosis.md`, ADR-0022 and its addendum, DT-231 and DT-232. Every statement below was checked against the code at commit `4a2ed00` and then re-checked after the changes of this step; a claim that a test "covers" a layer was accepted only when disabling the layer made a named scenario change (`tools/ci/mutation_safety.py`, results in `reports/ai/safety/mutation-service.json` and `docs/research/unsafe_speech_safety.md` section 6). Measured results are in `unsafe_speech_safety.md` (generated); this page is the reading of the design.

**Scope.** Speech is evidence for the debrief only. The deterministic engine is authoritative (ADR-0005); a critical act is credited from the verified action, never from speech; `speech_evidence` is an action of no behaviour. The question audited is: through which paths could words become *credited evidence*, and what stops each?

## 1. The path of one utterance

```
Unity (SpeechInterpretation.cs)           builds the context from the ENGINE's session, sends context + words
  -> POST /interpret (server/routers/interpret.py)      gate flag, rate limit, one model slot, audit
    -> safe_interpret (decide.py)                       any exception -> MENU, cancellation propagates
      -> Interpreter.interpret (decide.py)
           classifier gate  (ensemble.py -> embedding.py/hybrid.py, tfidf.py, guard.py, assertion.py)    names the candidate act and ranks the others
           critical branch  (decide.py)                 critical cue / critical act -> MENU pointing at the verified actions
           status           (status.py -> assertion.py) denied, hypothetical, reported, intended, other speaker, quoted, retracted, hedged, meta-question, contentless, incomplete, question, actual
           validate         (validate.py)               eligibility, critical, status, waiting question, waiting read-back, already credited, gate credited
           cap re-check, pending split, clarify once, abstain, menu
  <- OFFER_EVIDENCE | CLARIFY | ABSTAIN | MENU          (an OFFER is only an offer)
Unity applies an OFFER as one logged `speech_evidence` action
  -> SpeechEvidenceSubsystem (engine)                   shape, waiting question / read-back in the SESSION, stamps the running steps
  -> EvidenceStore.FromLog / SubmitUtterance (engine)   duplicate utterance, unknown act, critical act, per-act threshold, no running step, window, duplicate behaviour
```

Credit exists only when the last step accepts. Everything before it can only remove an offer.

## 2. The layers

Ids are those used by `tests/safety_challenge/taxonomy.json`. "Det." = deterministic code; "model" = depends on the embedding model or a trained head.

| id | layer (file) | responsibility | kind | what it detects, and how | duplicated by | tested by |
|---|---|---|---|---|---|---|
| H | hybrid head + TF-IDF head (`embedding.py`, `hybrid.py`, `tfidf.py`) | names the candidate act (semantic interpretation) | model | softmax over 19 classes from all-minilm:22m embeddings blended with TF-IDF; per-act threshold | the second reader (A4) | classifier benchmarks (synthetic) |
| A1 | assertion layer (`assertion.py`) | whether the speaker PERFORMS the act | det., lexical | 36 regular expressions (plus a test for contentless speech and one for another person doing it) over a speech-verb list and closed lists of roles, delay markers, hedges: denial, hypothetical, report, intention, another speaker, quotation, retraction, meta-question, hedge, unfinished action, fragment, contentless | used twice: in the gate (`ensemble.py`) and in `status_of` (`decide.py`) | `test_recall_fixes.py`, `test_safety_hardening.py`, the metamorphic sets, mutants E1, D5, A1, A2 |
| A2 | critical-content cue (`guard.py`, `content/ai/critical_cues.json`, allergen lexicon) | a sentence about identity, allergy, consent, fasting, worsening or handover is never credited under an ordinary act | det., lexical | regular expressions | `decide.py` critical branch; the head's own critical-act probability | mutants D1, H2, K1 |
| A3 | critical-mass cap (`embedding.credit_allowed`, `decide.py`; `critical_mass_max` 0.05 in `act_head_hybrid.json`) | refuses a sentence the head thinks is critical content said in some way | model + a number | sum of the head's probability over critical classes at or above the cap | gate AND `decide.py` | mutants H1, D2, DBL_cap |
| A4 | TF-IDF agreement (`ensemble.py`) | the second reader must name the same act | model | disagreement -> `HEADS_DISAGREE:<act>` | - | mutant E2 |
| A5 | per-act threshold (head; exported to the engine by `build_scoring.py`) | confidence floor per act | model + number | `probability >= threshold` | gate AND engine store (same numbers) | `test_recall_fixes.py`, `ScoringTests` T_EM3, mutant EN2 |
| A6 | critical-act and NONE exclusion (`credit_allowed`) | the head never credits a critical class or NONE | det. | membership test | `validate.py` (`entry.critical`), `decide.py` critical branch, engine store | mutants V1, D1, EN1 |
| V | deterministic validation (`validate.py`) | eligibility by behaviour window, critical entry, status vs act (a question is not an explanation), a waiting patient question for ANSWER_QUESTION, a waiting read-back for CLOSED_LOOP, behaviour already credited, gate required | det. | rule checks against the engine-built context | the engine store (window, duplicate, critical, threshold); the engine subsystem (waiting state, new) | mutants V1 to V7 |
| D | decision (`decide.py`) | critical -> MENU, NONE -> ABSTAIN, safety reasons -> ABSTAIN, one clarification then MENU, classifier unavailable -> MENU, exceptions -> MENU | det. | ordered rules | - | mutants D1 to D6, scenarios S11, S12, S16, S17 |
| R | route (`routers/interpret.py`) | feature flag, rate limit, one slot, audit with reason codes | det. | flag OFF -> MENU `FEATURE_OFF`, busy -> MENU, error -> MENU | Unity treats any failure as the menu | `server/tests/test_interpret_api.py` (needs the database container) |
| S | speech-evidence action (`SpeechEvidenceSubsystem.cs`) | shape; **new:** a waiting question / read-back in the session; stamps the running steps itself | det. | rejects malformed or unsupported evidence at the log | V (service side) | `InterpretationContextTests`, mutants ET1 to ET4 |
| E | evidence store (`Evidence.cs`) | the authority: unknown act, critical act, threshold, no running step, window, one credit per behaviour, one verdict per utterance id | det. | rule checks with the steps the engine stamped | - | `InterpretationContextTests`, mutants EN1 to EN6, `evidence-check` scenarios |
| M | content (`mapping.json`) | `speech_evidence` is listed in no behaviour's `actions`; critical acts are in no behaviour's `ai_acts` except SBAR_ELEMENT | det. | data | - | `test_safety_hardening.py::Content` |
| U | Unity client (`SpeechInterpretation.cs`) | builds the context from the engine session, applies an OFFER once, drops a cancelled or late answer, any failure is the menu | det. | EditMode tests (8) | - | **not mutation-run** (no Unity-free runner); the 8 tests are the check |

## 3. How unsafe speech could become credited evidence (paths examined)

1. **The head names an ordinary act for a non-performed sentence and nothing refuses it.** The only text-level barrier is A1 (twice). *Confirmed hole at `4a2ed00`:* 126 of 828 development and 64 of 385 challenge must-not-credit rows (15.2 and 16.6 percent) were accepted by the engine. Mechanisms: questions about whether to act, hedges, another person named with a speech verb other than say/tell, quotation, retraction, an unfinished action, a modal intention, a contentless utterance. Closed in this step; see section 5 for how far.
2. **A critical statement in words no cue covers.** Held by the head's own critical classes, the cap (A3) and A6, not by understanding. 0 of 29 development and 28 challenge critical rows were credited at `4a2ed00`, and 0 after. The unsafe sentences nearest the cap sit at 1.51 times its value; with the cap removed 8 of 9 cap-only sentences are accepted by the engine (mutant `DBL_cap`). The cap is *not* changed in this step.
3. **A client claims a waiting state it does not have.** ANSWER_QUESTION and CLOSED_LOOP need a waiting patient question or read-back; the flags come from the context the client sends. *Confirmed gap at `4a2ed00`:* the engine's store has no check for them (the CLI `evidence-check` accepts them), so the service was the only barrier and a tampered or stale client could claim the state. **Closed:** `SpeechEvidenceSubsystem` now checks the session itself (`InterpretationContextBuilder.PatientQuestionPending/ReadbackPending`) and rejects the action, so nothing is logged as evidence. The honest client is unaffected (Unity builds the context from the engine's own session).
4. **A client claims a window it is not in.** The engine ignores the claimed steps: the subsystem stamps `RunningSteps` and the store judges against them (mutants `EN4`, `BOTH_window`).
5. **The same words twice.** One credit per behaviour (store) and one verdict per utterance id (store). The duplicate-utterance check can only be observed for a *different* behaviour, because for the same behaviour the duplicate-behaviour check refuses it too (scenario `E07d` versus `E07b`): a redundancy, now documented.
6. **A critical act offered to the store.** Four of the five critical acts are in no behaviour's `ai_acts`, so the store refuses them as `UNKNOWN_ACT`; `CRITICAL_NEEDS_DETERMINISTIC_EVIDENCE` is reachable only for SBAR_ELEMENT. Both refuse; the reason string differs (scenario `E09_*`). Not a hole; a reading note.
7. **Service failure.** A classifier or embedder exception is the menu (`safe_interpret`, scenario S16); a cancellation (`KeyboardInterrupt`, `asyncio.CancelledError`) propagates and produces no decision (S17); a disabled gate, a busy slot and an unavailable model give the menu (route, D3). Unity drops a cancelled or late answer. *Gap documented:* the route has no wall-clock limit of its own (the embedder call has a 10 s timeout; Unity's request has `request.timeout`); the docstring that promised "a slow answer is the menu" was corrected to say so.
8. **Physical or procedural credit from speech.** Not possible by construction: no behaviour lists `speech_evidence` as an action (test), the store credits only `ai_acts`, and critical acts have no `ai_acts` entry but SBAR_ELEMENT, which the store refuses.

## 4. Why the recall tests did not catch the disabled-validation mutation

The mutant that disabled `validate` was caught by 9 of the 17 tests of `test_interpretation_regression.py` but by none of `test_recall_fixes.py`. Cause, verified by the new mutation matrix: the guards overlap on purpose, and every counterexample in `test_recall_fixes.py` is a non-performed sentence that the **gate's** assertion layer refuses before `validate` runs, so `validate` never decides anything on those inputs; a test that looks only at the final decision cannot tell which guard did the work. Fix: `tools/ci/mutation_safety.py` disables each guard (one textual edit that must match exactly once) in a copy and runs `tools/ci/safety_scenarios.py`, in which a stub gate that credits at full confidence stands in front of the guard under test, so that guard is the only barrier; layer scenarios call each guard directly; double mutants remove every service copy of one barrier and show what the engine does with the offer. 38 mutants (service, engine store, engine subsystem) were killed at the time of ADR-0023; 49 are killed after p1-ai-language-s17, which adds the route fallbacks and the store judged end to end; the table is in `unsafe_speech_safety.md` section 6.

## 5. Findings, status

| # | finding | evidence | status |
|---|---|---|---|
| F1 | The assertion layer missed whole classes of non-performed speech (see path 1) | 126/828 dev, 64/385 challenge accepted by the engine at `4a2ed00` | **Closed on the development and challenge rows; generalisation to the final set is measured once** (`unsafe_speech_safety.md`). Development-only changes removed 99 percent of dev failures but only 38 percent of challenge failures, so the first pass over-fitted wordings; the mechanisms were then generalised. |
| F2 | "Thank you." (a contentless utterance) was credited as ACKNOWLEDGE_FACT in the most permissive context | dev row | closed (closed list of discourse particles) |
| F3 | The audit row kept `validated(True)` for every decision and no reason: a refusal could not be counted by cause; reasons with a colon (`NOT_PERFORMED:denied`) would also have been stored as `UNCODED_REASON` | `routers/interpret.py` at `4a2ed00`, `middleware/audit.py` CODE rule | closed: `reason_codes()`, `validated(offer, codes)`, the rule-set version in `prompt_id`; no text is stored |
| F4 | The waiting-question / read-back flags were not re-checked by the engine | path 3 | closed in the engine |
| F5 | `"after you"` was a delay marker, so "I'll look after you" was read as a postponed intention (a false rejection that predates this step) | found while adding "we'll" | closed (idiom exempted); no safety weakening: it never denoted a delay |
| F6 | Mutation gap: single-guard mutants masked by overlapping guards | section 4 | closed (matrix, double mutants, engine mutants) |
| F7 | Near-term narration "I'll examine your tummy in a moment" (EXPLAIN_ACTION, whose definition IS announcing the action) is flagged as an intention by the delay marker "in a moment" | 3 development rows | **not changed**: the layer does not know the act, and relaxing it would weaken an assertion check without independent evidence; listed as a false-rejection cause |
| F8 | "Let me check ..." is read as another person acting (the pattern `let <someone> <verb>` has no exception for "me" and "us") | seen in the final set while checking a different thing (4 legitimate rows) | **deliberately NOT changed**: it was observed on the untouched final set, so fixing it now would be tuning on the final set. Recorded for a later configuration scored on fresh data. |
| F9 | A recogniser error that removes the negation ("I did not ask" -> "I ask") reads as a performed act | 18 of 28 development rows credited | **not mitigable by text**; reported separately, never counted as a pass; mitigation needs recogniser confidence or the verified action |
| F10 | Negation whose scope is a reason ("I did not want to rush, so I asked") is credited or refused by luck | 6 of 28 development rows credited | left as `ambiguous_review`: the label is for a person |
| F11 | An offer to repeat ("Shall I explain that again?") is credited as CLARIFY although it is a question about acting | benchmark label; 4 rows | kept (the benchmark and the definition treat it as a clarification); in the review sheet |
| F12 | Speaker attribution is lexical; the transcript carries no speaker identity | design | residual risk, in the taxonomy |
| F13 | The Unity client is not mutation-run | design | residual risk; the 8 EditMode tests stand |

## 6. Deterministic against model-dependent

Deterministic and mutation-tested here: A1 (as a function of text), A2, A6, V, D, S, E, M, the route's fallbacks (through `safe_interpret`). Model-dependent and therefore **only as good as their evidence**: H (the candidate), A3 and A5 (numbers set on synthetic dev data), A4. The evidence for A3 is a safety panel of 1,694 sentences and nine leaking sentences; the cap's margin is 1.51 times. No component of the credit decision reads a model's free text; the model only ranks acts.

## 7. Failure modes without independent evidence

* Every labelled figure in this repository for the speech path is synthetic and builder-written; none was labelled by a person independent of the builder (EX-01, DT-231). The labels of `tests/safety_challenge` are by construction and PENDING_HUMAN_REVIEW for every ambiguous or clinically relevant row.
* Real learner speech and real recogniser output differ from template speech in length, disfluency and word choice; the noise templates are small (`P5` filler words and `P6` a stutter; `P4`, lower case without punctuation, becomes identical to the plain row once normalised and was deduplicated away).
* The thresholds that the `speech_interpretation` gate would read (DT-229) are not approved by the owner; no live scoring is enabled.
