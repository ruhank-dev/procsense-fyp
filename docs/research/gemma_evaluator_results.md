# Gemma speech evaluator: development results (p1-ai-language-s22, ADR-0025)

**Status: SYNTHETIC DEVELOPMENT EVIDENCE.** Every sentence below was written by an AI assistant from frozen specifications and reviewed by the same assistant. It is not independent human validation, not real audio and not a headset result. The `speech_evaluation` feature stays **OFF**; the gate reads the independent set (EX-01, now paused) and a fresh frozen held-out set must score it before any claim is made.

## What was measured

The production path of ADR-0025 (`server/evaluate`, prompt `gemma-eval-4`, hash prefix `7ffff4fd3e7e`, model `gemma4:31b` on the free plan of Ollama Cloud) on `tests/synthetic_dev_v1` (493 items), then the engine's own graded communication score (`speech-score` of the CLI, the same C# code the simulator runs). The harness is `tools/synth_dev/evaluator_run.py` (raw replies stored) and `tools/synth_dev/evaluator_metrics.py` (recomputed without a model call). The prompt was changed twice after the first run, by category and never by row: `gemma-eval-3` added the approved annotation rubric rules verbatim and an information-versus-comfort rule; `gemma-eval-4` added grading anchors after the controlled partial-credit sets showed wrong or dismissive content graded too high. Three full runs, one per prompt version:

| Prompt | Valid replies | Legitimate credit recall (>= 0.5) | Credited precision | Items with a false credit (no-credit items) | Ordinary credit on critical content | Contrast sets correct (of 33) |
|---|---|---|---|---|---|---|
| gemma-eval-2 | 99.6% | 170/176 | 192/211 = 91.0% | 9 | 5 | 29 |
| gemma-eval-3 | 99.6% | 168/176 | 191/205 = 93.2% | 7 | 3 | 29 |
| **gemma-eval-4 (final)** | 99.2% (489/493) | **165/176 = 93.8%** | **188/204 = 92.2%** | **8** | **4** | **29** |

One sample of a stochastic model per version: differences of two or three items between versions are within run-to-run noise and must not be read as a trend. The final prompt was chosen for its partial-credit behaviour (below), not for these totals.

## Final run (gemma-eval-4), by the reporting list of the request

| Measure | Result |
|---|---|
| Objective: exact set match | 248/274 items whose gold has a performed objective |
| Objective: per-act recall / precision | 280/298 = 94.0% / 280/307 = 91.2% |
| Performed versus not performed | accuracy 98.4%; performed found 270/274; non-performed called performed 4/219 (negated 1/33, intended later 1/33, near miss 2/11; hypothetical, reported, other actor, thinking aloud, small talk, off topic, noise, critical-topic discussion 0 of 145) |
| Legitimate credit recall (final score >= 0.5) | 165/176 = 93.8% (per act 188/200 = 94.0%); at >= 0.9: 143/176 |
| Credited precision | 188/204 = 92.2% (155/163 = 95.1% at >= 0.9) |
| False credits on no-credit items | 8 items: 4 an ordinary objective on critical content, 2 an in-window objective for speech that is ineligible in the step, 1 negated and 1 intended-later read as performed |
| Wrong or extra objectives on eligible items | 8 (an extra legitimate-looking objective beside the right one; 3 at >= 0.9) |
| No-credit rejection | 309/317 |
| **Critical physical completion from speech** | **0** (the engine records and grades a critical objective for its small communication credit and never accepts it as evidence; 43 items had the model flag a physical-action claim, none produced physical credit) |
| Critical items | 91; the model flagged critical content on 64/91 and 8/402 non-critical items; critical act named performed on 51/54 claims; 0/30 critical-topic discussions called performed |
| Multi-intent (30 items, 54 credit acts) | 52 found at >= 0.5; performed set exact 28/30; no extra credit |
| Contrast sets | 29/33 correct (ordinary 25/28, critical 4/5); failing: two ASK_TOPIC pairs, one HANDOVER_CRITICAL_ELEMENT pair, one REASSURE pair |
| Window and scenario state (44 items) | model named the performed act on 40; out-of-window credited 0/29; absent waiting question or read-back credited 2/15 |
| Separation of scores | AUC 0.959; non-performed or irrelevant scoring >= 0.25: 3/189; legitimate scoring < 0.5: 10/146 |
| Schema | 489/493 valid after at most one retry; 4 invalid (SCHEMA 8 rejections, TIMEOUT 3); 7 items needed the retry; the four invalid give no judgement and no credit (the menu) |
| Latency | median 2.2 s per call, p95 5.8 s, max 14.0 s (3 parallel workers, free plan, desktop link) |
| Tokens | 5,043 per item (4,941 prompt, 101 output) |

Relevance, correctness and completeness distributions for the credit-eligible classes: relevance 3 on every performed finding; correctness 3 except one 0; completeness 3 on 117/136 clean items, 2 on 19. Score distributions (engine): legitimate performed mean 0.905, median 1.0 (136/146 at >= 0.5, 117/146 at >= 0.9, 10 at 0 of which 4 are the invalid replies); no-act items: 48/49 at 0; negated 27/28 at 0; intended later 27/28 at 0; hypothetical, reported and another actor 28/28 at 0.

Compared with the frozen Gemma-first experiment that preceded this work (same 493 items, one prompt, no engine integration, figures as counted in that experiment's own report): performed versus not performed 98.4% against 95.9%; false credits on no-credit items 8 against 26; ordinary credit on critical content 4 against 8; critical physical completion 0 against 0; schema validity 99.2% (489/493) against 100% (493/493); median latency 2.2 s against 2.3 s. **Recall went down:** the right objective was named as performed on 165 of the 176 credit-eligible items (93.8%) against 172 (97.7%) in the experiment. Four of the 11 misses are the invalid replies (no judgement, no credit by design); the other seven are items the model judged differently. The first full run of this task (`gemma-eval-2`) named 170; the later prompt versions, written to stop wrong content being graded high and to teach the rubric rules, gave up a few of them, within what one sample of a stochastic model can vary but in the same direction on two runs. The trade is fewer false credits for a lower recall and it is the owner's to weigh. The score-level recall (graded score of at least 0.5 in the engine) is the same count (165/176).

## Controlled partial-credit sets (synthetic development evidence)

Two small sets written for this purpose only (`tests/synthetic_dev_v1/partial_credit_v1.jsonl`, 32 items, and `partial_credit_v2.jsonl`, 32 items with different wording): for each of eight objectives a full, a partial (vague or incomplete), a wrong (relevant objective, false or dismissive content) and an irrelevant sentence. Final prompt: version 1 full 8/8, partial 8/8 (scores 0.4 to 0.8, never 0 and never full), wrong 6/8, irrelevant 8/8; version 2 28/32 correct. Failures seen in the item lists: a plainly false report-a-fault sentence ("working perfectly") credited in full; in version 2 two EXPLAIN_ACTION sentences (a vague "touch your chest" and one that contradicts itself) graded complete, and an irrelevant sentence about a vending machine taken as a fault report. They are left as they are: the sets are for behaviour, not for tuning.

## Remaining semantic failures (category level)

1. **Extra objectives**: a second legitimate-looking objective beside the right one (REPORT_FAULT with VOICE_ORDER, EXPLAIN_ACTION with EXPLAIN_TEST). These are the taxonomy boundary ambiguities of DT-241; the prompt teaches the contrasts but the definitions themselves overlap.
2. **Ordinary credit on critical content** (4): the speech is ordinary communication about a critical topic and the model also names an ordinary objective. The engine cannot tell this from legitimate multi-intent speech and does not try (no lexical veto); the instruction to treat critical topics separately is in the prompt.
3. **Modality slips** (2): one negation and one intended-later sentence read as performed. 4 of 219 non-performed items called performed is low and still not zero.
4. **Wrong content graded as correct** (rare): a flatly false statement that is on topic.
5. **Operational**: 0.8% of items end without a valid judgement (time-out or schema break twice); the fail-safe gives the menu and no speech credit.

## Free-plan usage

About 7.4 million tokens in about 2,100 calls over the whole task (three full runs of 2.0, 2.3 and 2.5 million tokens, partial-credit sets of about 0.15 million each, the earlier frozen experiment excluded). No paid API, no quota or rate-limit status (401, 402, 403, 429) was seen, no ceiling was reached. Each full run takes about 8 minutes with three workers.

## What this does not show

Nothing here is a generalisation claim: the same assistant wrote and reviewed the sentences, the prompt was tuned on category results of this set, there is no real ASR noise and no trainee. The next step is a fresh held-out set frozen before it is scored, then real speech on the Meta Quest path (DT-240).

## Fresh held-out acceptance run (21 October 2026): NOT APPROVED by the pre-registered criteria, owner decision requested

**Status: AI-written and AI-reviewed synthetic held-out data, scored once. Not independent human validation, not real audio, not a headset result.** The feature is not marked complete: three non-blocking criteria missed, and the pre-registered rule says the owner decides then. Nothing was tuned, rerun or relaxed after the result.

**Set (`tests/synthetic_heldout_v1`, `tools/synth_heldout/`).** 259 items from specifications frozen before any sentence was written (SHA-256 manifest): the reserved personas P5 (formal) and P7 (calm, methodical) only, four reserved topic groups (cataract and eye drops, dental extraction, hand surgery under an arm block, varicose veins and leg compression), new objective seeds and new multi-intent pairs; 102 credit-eligible, 157 no-credit, 114 items in 19 contrast sets (14 ordinary, 5 critical), 50 safety-sensitive, 18 multi-intent, 18 window/state items. Plus a 32-item controlled partial-credit addendum (8 objectives, one full, partial, wrong and irrelevant item each, expected score bands fixed beforehand). Deterministic checks: no exact or near copy (word-set Jaccard 0.7) of any of 15,884 sentences in the repository's test sets, no duplicates, opener spread. Sentences written once and reviewed once before the run (3 multi-intent rewrites, 3 near-copy rewrites, about 40 opener rewrites for variety; nothing after the run). The prompt (`gemma-eval-4`, hash `7ffff4fd3e7e`), the code, the data and the criteria (`acceptance.json`) were hashed in `eval_config.json` before the first call; a second scored run is refused (`RUN_LOCK.json`). `gemma4:31b` on the free plan, 296 calls, 1.46M tokens, no quota error, no paid API.

| Criterion (pre-registered) | Observed | 95% Wilson | Result |
|---|---|---|---|
| A1 critical physical completions credited from speech (blocking) = 0 | **0** (19 items had the model flag a physical-action claim) | | pass |
| A2 legitimate credit recall >= 0.90 | **98/102 = 96.1%** | 90.4 to 98.5 | pass |
| A3 credited precision >= 0.95 | **111/120 = 92.5%** | 86.4 to 96.0 | **miss** |
| A4 non-performed items that earn credit = 0 | **2** (of 124) | | **miss** |
| A5 critical-content items on which an ordinary objective earns credit = 0 | **4** (of 50) | | **miss** |
| A6 performed versus not performed accuracy >= 0.95 | **97.3%** (performed found 133/135, non-performed called performed 5/124) | 94.8 to 99.6 | pass |
| A7 valid replies after the retry >= 0.995 | **258/259 = 99.6%** | 97.9 to 99.9 | pass |
| A8 partial-credit items in their expected band >= 0.80 | **27/32 = 84.4%** (full 7/8, partial 5/8, wrong 7/8, irrelevant 8/8) | 68.3 to 93.1 | pass |
| A9 median response time <= 4 s | **2.7 s** (p95 4.4 s, max 11.7 s) | | pass |

**Other results (no pass mark).** Exact objective set 121/135, per-act recall 139/149 and precision 139/156; multi-intent: 31 of 32 acts found, 17 of 18 sets exact, no extra credit; contrast sets 16/19 fully correct (ordinary 12/14, critical 4/5; failing: ACKNOWLEDGE_FACT, CONFIRM_IDENTITY, REASSURE); window and state: 0 of 14 out-of-window and 0 of 4 state-absent items credited; score separation AUC 0.970; mean score full 0.875, partial 0.65, wrong 0.125, irrelevant 0.0; 5,019 tokens per item.

**What the misses are (reported, not tuned away).**
* A3, 9 wrong credits: the right objective plus a second one the gold does not list (a REASSURE beside an ANSWER_QUESTION, an EXPLAIN_PROCEDURE beside a REASSURE: the boundary ambiguity of DT-241), CLARIFY read as ASK_TOPIC, all four ordinary credits on critical content (A5), and the two non-performed items (A4). Four eligible items were missed (CLARIFY as ASK_TOPIC, two REASSURE read as ANSWER_QUESTION, one CHECK_UNDERSTANDING with no act).
* A4, 2 items: "I'm not saying the dentist does this all the time, and I can't promise the area will be numb" credited as REASSURE (a poorly separated negation, arguably an ambiguous sentence of this set) and "The pharmacist wrote that you use drops only for dry eyes" credited as ACKNOWLEDGE_FACT (a reported source that also states the fact: DT-242's reported-versus-other-actor family).
* A5, 4 items: STATE_ALLERGY questions credited as ASK_TOPIC (twice), CONFIRM_IDENTITY with an EXPLAIN_ACTION beside it, SBAR_ELEMENT with a VOICE_ORDER beside it. In every case the critical behaviour was NOT credited and no physical completion followed; the extra objective earns its own credit in its own window. The same family appeared in the development set (4 of 91).

**Comparison with development.** Recall 96.1% (dev 93.8%), performed versus not performed 97.3% (98.4%), precision 92.5% (92.2%), valid replies 99.6% (99.2%), median latency 2.7 s (2.2 s), critical physical completions 0 and 0. The held-out numbers are in the same range as the development numbers, which is what a held-out set is for: the development result was not an artefact of tuning. The strict zero-tolerance criteria A4 and A5 and the 0.95 precision mark are the engineer's proposals (DT-245) and are exactly what they were before the run.

**Verdict by the pre-registered rule: NOT APPROVED (owner decision).** The blocking safety criterion passed; recall, performed-versus-not, schema, partial-credit behaviour and latency passed; precision and the two zero-tolerance criteria did not. The owner's options (DT-246): (a) accept the architecture as complete with the thresholds revised in the open and the reasons recorded (for example precision at least 0.90 and a small tolerance for non-performed credit, noting that no critical behaviour is ever credited from speech); (b) one more prompt iteration judged on a NEW held-out set, since this one must not be used again; (c) accept the software and rely on the real Meta Quest and ASR test (DC-10, Sannan) for the final word. The engineer's recommendation is (a) or (c), because the safety invariant holds by construction in the engine and the remaining misses are overlapping definitions, which only the owner can settle.


## Final held-out v2 (21 October 2026): one pre-registered criterion missed by one item, owner decision requested

**Status: AI-written and AI-reviewed synthetic held-out data, scored once. Not independent human validation, not real audio, not a headset result.** After held-out v1 the owner chose one final prompt refinement and one brand-new held-out set. Nothing was changed after the v2 result; v2 is not scored again and no v3 was created.

**The refinement (prompt `gemma-eval-5`).** Only the approved rubric and general failure categories: a second-objective test (a second objective is reported only when the words left after removing the first still perform it by definition; reasons, timing phrases, the subject of an act and the content of an order, question or handover line are part of the first act), written boundary decisions for REASSURE, ACKNOWLEDGE_EMOTION, the information objectives, CLARIFY versus ASK_TOPIC and ACKNOWLEDGE_FACT versus reported speech, a denial that partly restates its content is negated, the rubric's critical-objective precedence (an allergy question is STATE_ALLERGY only, a name and birth date request is CONFIRM_IDENTITY only, the content of a handover line is not also an order), the rule that a genuine separate clause is still credited next to a critical one, and 13 new original worked examples. No lexical veto, no change to the engine or the scoring, no sentence or wording taken from a failed v1 item (a test checks that no worked example is a row of the development set or of either held-out set). Dev sanity check of the new prompt (development data, not evidence): performed versus not performed 99.2%, recall 168/176, precision 191/201 = 95.0%, ordinary credit on critical content 0 of 91, contrast sets 31/33.

**Criteria, fixed before v2 existed (`tests/synthetic_heldout_v2/acceptance.json`, ADR-0025 section "Criteria for held-out v2").** The core targets were not lowered. The two zero-tolerance marks of v1 became rules tied to the approved semantics: non-performed items earning credit at most 1% (D1), unjustified ordinary credit on single-critical items at most 3% (D2), and a new check that a critical act and an independent ordinary act in separate clauses give the ordinary credit and never the critical behaviour (D3).

**Set.** 244 items from frozen specifications: new topic groups (endoscopy with sedation, shoulder arthroscopy with a nerve block, skin lesion removal under local, ear surgery), personas P5, P7 and the new P8 and P9; 96 credit-eligible, 148 no-credit, 114 items in 19 contrast sets, 54 safety-sensitive (including 8 mixed critical plus ordinary items), 26 multi-intent, 18 window and state items. No exact or near copy (Jaccard 0.7) of any of 16,175 committed test sentences, development and held-out v1 included. Free plan, 281 calls, 1.71M tokens, no paid API.

| Criterion (pre-registered) | Observed | Result |
|---|---|---|
| B1 critical physical completions credited from speech = 0 (blocking) | **0** (13 items flagged a physical-action claim) | pass |
| B2 out-of-window or state-less items whose named objective earns credit = 0 (blocking) | **0** of 18 | pass |
| C1 legitimate credit recall >= 0.90 | **95/96 = 99.0%** (Wilson 94.3 to 99.8) | pass |
| C2 credited precision >= 0.95 | **109/109 = 100%** (96.6 to 100) | pass |
| C3 performed versus not performed >= 0.95 | **99.2%** (performed found 124/124; non-performed called performed 2/120, both thinking aloud, neither credited) | pass |
| C4 valid replies >= 0.995 | **244/244 = 100%** on the main set | pass |
| C5 partial-credit items in their expected band >= 0.80 | **25/32 = 78.1%** (full 5/8, partial 6/8, wrong 6/8, irrelevant 8/8; Wilson 61.2 to 89.0) | **miss by one item** |
| C6 median response <= 4 s | **2.3 s** (p95 4.0 s, max 7.6 s) | pass |
| D1 non-performed items earning credit <= 1% | **0 of 95** (negated, planned, hypothetical, reported, another actor; ordinary and critical) | pass |
| D2 unjustified ordinary credit on single-critical items <= 3% | **0 of 40** | pass |
| D3 mixed critical plus ordinary items: ordinary act credited | **8/8** (the critical behaviour was never credited) | pass |
| E1 multi-intent per-act recall >= 0.90 | **40/40** | pass |
| E2 multi-intent items with an extra credit <= 1 | **0** | pass |
| E3 contrast sets fully correct >= 0.85 | **19/19** (ordinary 14/14, critical 5/5) | pass |

**Ordinary credit on critical-content speech, case by case.** Exactly 8 items: the 8 mixed items, each with a specification that lists an independent ordinary act in a separate sentence (identity request plus announcing a sensor, allergy question plus a history question, consent request plus a read-back request, handover plus an order for a trolley, background line plus a fault report, identity request plus a remark on nervousness, allergy question plus a promise of care, consent question plus a promise to stay). All 8 are justified by the specification and none credited the critical behaviour. On the 40 single-critical items (genuine, denied, planned, hypothetical, reported, another actor, discussion) no ordinary objective was credited at all.

**Other results.** Exact objective set 122/124, per-act recall 144/146 and precision 144/148 (the extra objectives were not credited), per-objective recall 100% on 18 of 19 objectives (CHECK_UNDERSTANDING 4/5), per-situation false credits 0 everywhere, score separation AUC 0.993, schema first-attempt failures 1, tokens 6,097 per item.

**The one miss, in full.** Seven partial-credit items fell outside their band. Three were `full` items (long, complete sentences of about 25 to 30 words) on which the model's reply was rejected twice by the strict contract (SCHEMA: the quoted evidence was longer than the 160 characters the contract allows, and once an objective name was invalid), so the item earned nothing: a robustness limit of the contract for long utterances, not a semantic error. Two were `wrong` items graded as correct (a false statement that the shoulder operation is done awake through the back, and a sentence announcing a sensor while denying touching the ear), the weakness already seen in development. Two were `partial` items graded complete ("You look a bit tense.", "All understood?"), vague but complete as a single act. The main set never hit the length limit (median sentence 11 words, longest 21).

**Comparison at the aggregate level.**

| | Development (v4 / v5 sanity) | Held-out v1 (v4) | Held-out v2 (v5) |
|---|---|---|---|
| Items | 493 | 259 | 244 |
| Critical physical completions | 0 / 0 | 0 | **0** |
| Legitimate recall | 93.8% / 95.5% | 96.1% | **99.0%** |
| Credited precision | 92.2% / 95.0% | 92.5% | **100%** |
| Performed versus not performed | 98.4% / 99.2% | 97.3% | **99.2%** |
| Non-performed credited | 2 / 1 of 219 | 2 of 124 | **0 of 95** |
| Ordinary credit on critical content | 4 / 0 of 91 | 4 of 50 | 0 unjustified of 40 (8 justified) |
| Contrast sets | 29/33 / 31/33 | 16/19 | **19/19** |
| Valid replies (main set) | 99.2% / 99.4% | 99.6% | **100%** |
| Median response | 2.2 s / 2.5 s | 2.7 s | **2.3 s** |
| Partial-credit band accuracy | 28/32 (v2 set, v4) | 27/32 | 25/32 |

The refinement helped on every semantic measure and the held-out v2 result is the best of the three. It is not a clean comparison: the three sets differ in topics, personas and mix, the prompt differs between the columns, and v2 was authored after v1's failure categories were known (the authoring rules for REASSURE, ANSWER_QUESTION and single-critical items were fixed first, which is part of what v1 taught). The development sanity run is not evidence of generalisation.

**Verdict by the pre-registered rule: NOT APPROVED (owner decision).** Both blocking criteria and every other criterion passed; the partial-credit criterion missed by one item. The rule was written to stop here and ask. The owner's options (DT-246, DT-247): (a) approve the architecture as complete with the 78.1% recorded as an accepted exception, because 3 of the 7 misses are a contract limit on long evidence that does not touch credit safety and the main set, which carries the safety and precision claims, passed everything; (b) authorise a small contract-level fix (allow or shorten long evidence) and re-score ONLY the 32-item partial-credit addendum, a fresh partial-credit set rather than the same items; (c) leave it as it is until the real Meta Quest and ASR test (DC-10, Sannan). The engineer recommends (b) or (a): the behaviour the criterion is about (full, partial, wrong, irrelevant) is mostly right, the length limit is a real defect for natural long speech, and it should be fixed before a headset test.


## Final approval (22 October 2026): contract fix for DT-247 and the fresh partial-credit set v3

**Status: AI-written and AI-reviewed synthetic data, each set scored once against criteria fixed beforehand. Not independent human validation, not real audio, not a headset result (DC-10, Sannan).** The owner froze the architecture, the `gemma-eval-5` prompt, the scoring semantics and the thresholds, accepted the held-out v2 semantic results as the final semantic evidence, and asked for one thing: fix the genuine contract defect found in v2 (DT-247) and check it on a fresh 32-item partial-credit set, approving the feature if at least 80% of its items land in their expected band.

**The fix.** v2's replies for two long full-credit sentences were rejected twice because the quoted evidence was 198 and 163 characters against a flat limit of 160; a third reply was rejected for a different reason (the model named ANSWER_QUESTION twice, a duplicate objective the contract rightly refuses, not changed). The evidence limit is now `MAX_EVIDENCE` = 400 characters, the longest utterance the route accepts and the engine's `MaxSpan`, and the evidence must still be words copied from the utterance. Nothing else in the contract, the prompt text or hash, the engine's rules or the scoring changed. Tests: a long utterance can be quoted whole, an invented or over-long quote is still refused, the rationale keeps its own limit, short replies are judged as before, the three caps are the same number, the engine accepts a 400-character span and refuses 401, and a mutation test removes the cap check.

**The set (`tests/synthetic_partial_v3`).** 8 objectives (EXPLAIN_PROCEDURE, EXPLAIN_TEST, EXPLAIN_ACTION, REASSURE, ACKNOWLEDGE_EMOTION, ANSWER_QUESTION, CHECK_UNDERSTANDING, REPORT_FAULT) times full, partial (vague or incomplete but correct), wrong and irrelevant: 32 items, 10 of them longer than 160 characters and naturally rambling, new topics (toenail surgery under local, MRI), no reuse of any earlier sentence (checked against 1,627 committed set sentences plus the earlier addenda). Criteria (`acceptance.json`): at least 80% of items in band (full >= 0.9, partial 0.1 to 0.9, wrong <= 0.25, irrelevant <= 0.1) and no reply rejected for long evidence; the data, criteria, prompt, contract and engine cap were hashed before the run; one scored run; free plan, 32 calls, 194,125 tokens.

| Criterion | Observed | Result |
|---|---|---|
| P1 items in expected band >= 0.80 | **27/32 = 84.4%** (Wilson 68.3 to 93.1): full 8/8, partial 5/8, wrong 6/8, irrelevant 8/8 | pass |
| P2 replies rejected for long evidence = 0 | **0**; valid replies 32/32, all 10 long items valid and 9 of them in band | pass |

Mean score by band: full 1.125 (a full item with an extra credited objective), partial 0.82, wrong 0.23, irrelevant 0.0; median response 2.7 s. The 5 misses are the known weaknesses, not the contract: two wrong-content sentences graded as correct (a false statement that a toenail operation is done asleep through the groin; a sentence denying a touch while announcing one) and three vague sentences graded complete ("You seem a bit worried.", "Does that all make sense?", and a rambling "it will all be fine" comfort).

**Verdict: APPROVED.** The contract fix and the CI pass, the fresh set reached 84.4% (target 0.80, unchanged), and held-out v2's one miss is closed by this follow-up. The final evidence the gate reads is `reports/ai/speech-evaluation-acceptance.json`: held-out v2 for the semantic criteria (recall 99.0%, precision 100%, performed versus not performed 99.2%, valid replies 100%, 0 critical physical completions, 0 window/state bypass, no non-performed or unjustified critical-content credit, multi-intent 40/40, contrast sets 19/19, median 2.3 s) and set v3 for the partial-credit bands. The feature is done: it is not optimised further, no further synthetic sets are made, and the next evidence is the real Quest and ASR check (DC-10).
