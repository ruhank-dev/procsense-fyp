# AI in ProcSense: what exists, what is missing and what is planned

Audit of 12 October 2026. It answers one question for every AI idea in the original plans: **is it built, missing, deferred or only planned, and is the thing that is built really AI or just rules?** Nothing here claims more than the code and tests show. The plans it was read from are `docs/plans/p1/`, `docs/plans/p2/`, the Master Plan (AI sections 81 to 88) and the Implementation Guide (B-R2, B-R5, B-R8 to B-R10, B-M3, B-M5, B-M6, B-M7).

## 0x. Update of 22 October 2026 (speech evaluation APPROVED; the architecture is final)

* **Final architecture:** ASR, Gemma semantic evaluation (`gemma4:31b`, prompt `gemma-eval-5`, frozen), authoritative simulator state and evidence, deterministic graded scoring, VR feedback (ADR-0025). Gate `speech_evaluation`: ON (reads `reports/ai/speech-evaluation-acceptance.json`).
* **Closing step:** the DT-247 contract fix (evidence up to 400 characters, still copied from the utterance) and a fresh frozen 32-item partial-credit set scored once: 27/32 = 84.4 percent in band, 32/32 valid replies, 0 length rejections. Together with held-out v2 (recall 99.0 percent, precision 100 percent, 0 critical physical completions, contrast sets 19/19) every acceptance criterion is met. s22 and C37 are closed; DT-245 to DT-247 completed.
* **Not claimed:** independent human validation, real audio or a headset result. Still open and outside the software acceptance: the real Meta Quest and ASR end-to-end check (DC-09 and DC-10, Sannan), the transcript privacy decision (DT-248) before real trainee speech is sent to Ollama Cloud, the taxonomy boundary decisions (DT-241 to DT-244).

## 0w. Update of 21 October 2026 (held-out v2: one criterion short by one item, owner decision)

* **What was done:** one prompt refinement (`gemma-eval-5`, approved definitions and general categories only, no veto) and one brand-new frozen held-out set (244 items and a 32-item partial-credit addendum), scored once against criteria written beforehand.
* **Result:** 0 critical physical completions, 0 window/state bypass, recall 99.0 percent, precision 100 percent, performed versus not performed 99.2 percent, valid replies 100 percent, non-performed credits 0 of 95, unjustified ordinary credit on critical content 0 of 40, mixed critical plus ordinary items 8/8, multi-intent 40/40, contrast sets 19/19, median 2.3 s; partial-credit bands 25/32 = 78.1% against 0.80.
* **Verdict:** NOT APPROVED by the pre-registered rule, owner decision DT-246; a contract limit on long evidence (DT-247) caused 3 of the 7 band misses. Nothing was ticked; v2 is not scored again. Details: `docs/research/gemma_evaluator_results.md`.

## 0v. Update of 21 October 2026 (final held-out acceptance: not approved, owner decision)

* **Set and rule:** a fresh frozen held-out set (259 items and a 32-item partial-credit addendum; reserved personas P5 and P7, four reserved topic groups; AI-written, AI-reviewed, synthetic) scored once with the unchanged architecture against criteria hashed beforehand. Result: 0 critical physical completions, recall 96.1 percent, performed versus not performed 97.3 percent, valid replies 99.6 percent, partial-credit behaviour 27/32, median 2.7 s; precision 92.5 percent (0.95 proposed), 2 non-performed credits (0 proposed), 4 ordinary credits on critical content (0 proposed).
* **Verdict:** NOT APPROVED, owner decision DT-246; nothing was ticked complete and nothing was tuned. `docs/research/gemma_evaluator_results.md` has the table and the reasons. The real Meta Quest and ASR end-to-end test is DC-10 in `docs/testing/SANNAN_EXTERNAL_TESTING.md`.

## 0u. Update of 21 October 2026 (the target architecture: Gemma semantic evaluation, engine-scored; the classifier stack is retired, ADR-0025)

**Read this first: the sections 0h to 0t below describe the classifier, critical-mass cap and semantic-reader architecture as it was; ADR-0025 replaced it and its code was removed. They stay as the history of why.**

* **Pipeline:** ASR transcript, then Gemma semantic evaluation (`gemma4:31b` on Ollama Cloud, free plan; strict JSON judgement of who spoke, whether it happened now, assertion and modality, the objectives actually performed, relevance, correctness, completeness and physical-action claims), then the engine's authoritative state and evidence checks, then a deterministic graded score, then VR feedback. Gate `speech_evaluation`: OFF, SKIPPED (no independent result file).
* **Removed:** the hybrid-head classifier and both heads, the two-head agreement gate, the critical-mass cap and its policies, critical cues, the assertion layer, the interpretation service, the earlier semantic reader and critical verifier, per-act thresholds, `/classify` and `/interpret`, the classifier benchmark, recall, cap, NLI, rubric and safety-gate harnesses with their tests, the classifier mutation catalogue, and the `classifier*` and `speech_interpretation` gate features. **Kept:** the engine's state, window and evidence checks, physical-action verification, the process scoring, the clinical and scenario content, the taxonomy and mapping, the frozen unsafe-speech sets with their hash protection, ASR and speech orders, every result file and research page as history, the development set and its tooling.
* **Result on the 493-item development set (synthetic, AI-written and AI-reviewed, NOT independent):** performed versus not performed 98.4 percent; legitimate credit recall 165/176 and credited precision 188/204 (at a graded score of at least 0.5); 8 items with a false credit on no-credit items (26 in the isolated experiment, as counted there) and 4 ordinary credits on critical content (8 there); 0 critical physical completions from speech; 489/493 valid replies; median 2.2 s, p95 5.8 s, about 5,000 tokens per item (`docs/research/gemma_evaluator_results.md`). Two controlled partial-credit sets show full, partial and irrelevant speech graded as intended and wrong content sometimes graded too high.
* **Open:** a fresh frozen held-out set scored once, real speech on the Meta Quest (DT-240), the owner's approval of the gate thresholds and the experimental graded rubric (DT-245), the transcript privacy decision (DT-248), the taxonomy boundary decisions (DT-241 to DT-244), scene wiring (Sannan, DC-09).

## 0t. Update of 21 October 2026 (baseline, Gemma reader and Gemma-first experiment on the 493-item development set)

* **Shipped baseline** (tests/synthetic_dev_v1, development only): recall 47.7 percent, precision 84.8 percent, 13 false credits, 0 of 91 critical false offers or credits, 95.9 percent of no-credit items not offered; the critical-mass cap, the critical-topic menu and the objective confusions of the head are the main limits (`tests/synthetic_dev_v1/baseline_report.md`).
* **Existing Gemma reader** (ADR-0024, cap hard): raw reading right on 140 of 176 eligible items, but the gates leave 77 credited; 84 of the 99 misses are understood-but-blocked (`gemma_comparison.md`).
* **Isolated experiment, Gemma-first evaluator** (`tools/experiments/gemma_first/`, prompt/parser/scorer frozen by hash; weights EXPERIMENTAL; production untouched): 172 of 176 eligible items credited, 54 of 54 multi-intent acts, 0 physical completions, but 26 false credits (negation is the weak point) and ordinary credit on 8 critical items; recommendation pending the owner's decision (`gemma_first_comparison.md`).

## 0s. Update of 21 October 2026 (synthetic-data factory abandoned; direct controlled synthetic development data)

* **Owner decision:** human effort is reserved for the final Meta Quest end-to-end test; the manual EX-01 author/annotator workflow stays paused (tooling kept). A multi-model cloud synthetic-data factory was tried in three bounded free-plan pilots (2.75, 25.6 and 23.1 percent accepted) and **abandoned** as too costly in time and cloud usage for its purpose; its code, tests, reports, pilot artefacts and registrations were removed.
* **Kept:** the diversity and leakage helpers (`tools/synth_dev/diversity.py`) and the findings that concern ProcSense itself: taxonomy boundary ambiguities (DT-241), speech-form semantics left open (DT-242: broken-off, hedged performance, performed-versus-hypothetical tail, reported versus another actor), delegated communication (DT-243), claims of earlier completion and the possible guard gap (DT-244).
* **New direction (approved):** a 500-item **development** set written directly (AI-written, AI-reviewed by the same model, never independent or human-labelled), with every specification (objective, situation, actor, window, state, expected credit) derived from the approved rules and frozen before any sentence is written, contrast sets, deterministic validation, no cloud call. Design: `docs/research/direct_synthetic_dev_set_design.md`. A fresh held-out set (personas P5 and P7, reserved topic groups) comes later and is frozen before scoring; the real validation remains the Quest test (DT-240). DT-243 and DT-244 stay excluded.
* **Unchanged:** `cap_policy=hard`, both switches OFF, cue, thresholds, scoring authority, verifier non-authoritative, the 90 percent recall target.

## 0r. Update of 21 October 2026 (EX-01 phase two decisions applied)

* **Decided by the owner:** 300 risk-weighted items; real-ASR coverage 0 here (real ASR is a separate later phase, no invented typos); each target reported as a point estimate AND a 95 percent Wilson interval (the lower-bound claim kept apart), zero counts as observed counts with an upper bound; no names assigned.
* **Prepared:** author packages, combine, near-copy leak check, the pre-freeze dataset and reviewer summary, the independence designs (strict five-person; four-person cross-annotation as the smallest defensible alternative; three-person not recommended). Nothing is authored, labelled or run; the safe configuration is unchanged. `docs/testing/ex01_semantic_evaluation_plan.md`, `docs/testing/ex01_role_designs.md`.

## 0q. Update of 21 October 2026 (optimisation stopped; EX-01 phase two prepared)

* **Stopped:** further tuning against dev, test and test2. The valid configuration is unchanged: `cap_policy=hard`, both switches OFF, thresholds, cue and engine unchanged, the verifier experimental only. Targets unchanged: recall at least 90 percent, precision at least 95 percent, 0 critical and 0 adversarial false credits, schema at least 99.5 percent, no critical credit from speech.
* **Recorded accurately (development evidence, not validation):** 95/140 current valid hard pipeline; 114/140 verifier-gated is hypothetical; 113/140 corroborated cap is research-only and failed the live panel; the isolated verifier detected 34/34 critical and cleared 35/42 legitimate sentences on a non-held-out experiment.
* **Prepared (no data):** the EX-01 phase two plan, 17 coverage families, a blind labelling kit with roles record and a freeze, a layered evaluation tool that separates reader understanding from downstream rejection, guides and templates. The first scored run waits for the owner's people, the freeze, the dataset and reviewer summary, and approval. `docs/testing/ex01_semantic_evaluation_plan.md`.

## 0p. Update of 21 October 2026 (the 90 percent target: what moved, what did not)

* **New engineering target:** legitimate recall at least 90 percent (126/140 on the current set), credited precision at least 95 percent, 0 critical and 0 adversarial false credits, schema compliance at least 99.5 percent, no critical credit from speech alone. Not lowered, no guard weakened.
* **Done:** the engine's waiting-question and waiting-read-back state is given to the reader as context (dev estimate 92 to 95 of 140 under the hard cap, the same as the shipped path); the historical timeouts answer; an isolated critical-claim verifier was built and measured once (critical 34/34 detected, legitimate 35/42 cleared) and is NOT integrated; ASR normalisation was explored and rejected.
* **Not reachable on these datasets** with the present independent checks: the cap, the engine thresholds, the critical cue and contested labels bound the recall (see the evolution table). The next meaningful evaluation is on independent labelled data (EX-01), with DT-239 deciding any verifier-gated lift. `cap_policy` stays hard; both switches OFF. `docs/research/recall_improvement_stage2.md`.

## 0o. Update of 21 October 2026 (the corroborated cap: implemented OFF, the bounded live check failed)

* **Implemented (p1-ai-language-s19):** a critical objective in the model's reading, under any verdict, is the menu; the setting `interpret_llm_cap_policy` (default `hard`) can bypass the critical-mass cap only for a corroborated candidate. 115 semantic tests, 29 mutants killed; the frozen set untouched.
* **Replay (recorded replies, design data, exploratory):** dev live 92 to 113 of 140, dev recorded 88 to 110, test 75 to 95, test2 75 to 104; no row lost; 0 critical, 0 adversarial, 0 out-of-window offers; the 67 historical raw false credits all still blocked.
* **Bounded live panel (gemma4:31b, free plan, 12 calls, 17,988 tokens):** 5 of the 12 sentences that only the model's critical report can stop were OFFERED, all critical-content sentences. **The corroborated policy fails and stays OFF; do not enable it.** The 85 percent recall target stays NOT MET. Open decisions A to D: `docs/research/corroborated_cap_results.md`.

## 0n. Update of 21 October 2026 (failure attribution of the semantic reader: analysis, a proposal and decisions requested)

* **Explained:** the earlier guarded evaluator's 118/140 and the live pipeline's 92/140 differ by 34 rows lost and 8 gained; 27 of the 34 are refused by the head's critical-mass cap, which the earlier evaluator did not have; in 30 of the 34 the model's reading was right (`docs/research/failure_attribution.md`).
* **Independent evidence found:** the model's own critical-objective report flags 113 of 120 critical rows and 3 of 420 legitimate rows; its misses are exactly the rows the cap and the dominance of the critical mass catch.
* **Proposed, not implemented:** a corroborated cap behind `cap_policy` (default hard), estimated at +21 rows on dev (to 113/140) and +20 and +29 on test and test2 in recorded data with no previously observed unsafe credit reopened; an unresolved residual risk (8 panel sentences) needs a small live check first. Decisions are requested in `docs/research/semantic_reader_architecture_proposal.md`. `speech_interpretation` and `interpret_llm_enabled` stay OFF.

## 0m. Update of 20 October 2026 (Ollama Cloud semantic reader: release candidate behind OFF switches)

* **Platform:** Ollama Cloud is the chosen inference platform (ADR-0024). Verified 20 Oct: the cloud route enforces no JSON schema (official page and a controlled test), so structured output is Pydantic-validated, strict, never repaired, with bounded retries and a fallback to the shipped decision. No Ministral model and no Qwen3.5 397B are in the cloud list.
* **Pipeline:** the model is a reader whose SUPPORTED finding is a candidate behind the shipped deterministic checks and the head's critical-mass cap; critical topics and non-performed sentences never reach it; the model can veto an offer; the engine's evidence store stays the authority. 62 tests and 17 mutants.
* **Measured, synthetic, by replay:** 0 critical, 0 adversarial and 0 out-of-window offers on dev, test and test2; recall 88, 75, 75 of 140 against 95, 76, 79 for the shipped path; strict validity of recorded replies 92.2, 91.4, 78.3 percent. Recall (85%) and schema (99.5%) targets NOT MET; the cap and the cue menu set the recall ceiling (DT-238).
* **Live runs (20 Oct, approved):** the model comparison is BLOCKED (`deepseek-v4.1-flash` needs the paid plan, HTTP 402; `mistral-large-4` is not rolled out, HTTP 403; no paid plan enabled), so no winner was chosen; the live `gemma4:31b` run on 488 dev rows sent 141 rows to the model: 137 of 137 replies valid, 4 timeouts (defect fixed), recall 92/140 at 92/93, 0 critical, 0 adversarial, 0 out-of-window offers; shipped path 95/140. No model is ready.
* **Free-plan comparison (20 Oct, approved):** `gpt-oss:120b` 79/140 (99.3% valid) and `nemotron-3-super` 88/140 (79.4% valid, 3.5 s median) against `gemma4:31b` 92/140; 0 observed unsafe offers for all; Gemma stays the strongest viable free option, no model is ready.
* **Not done:** any further model (needs approval, DT-249), the privacy decision (DT-248), independent validation (EX-01), real audio, Quest (Sannan). `speech_interpretation` and `interpret_llm_enabled` stay OFF.

## 0l. Update of 20 October 2026 (Ollama Cloud `gemma4:31b` as a semantic reader: experiment only)

* **Experiment, not a decision:** `docs/research/cloud_gemma_rubric.md` (numbers in `reports/ai/cloud-gemma-rubric.json`, adapter `tools/bench/cloud_eval.py`). On `tests/action_bench_v1` dev, test and test2, Gemma with the production guards credited 118, 121 and 111 of 140 legitimate sentences against the benchmark baseline gate's 83, 66 and 69 (not the shipped interpretation path, which reaches 95, 76 and 79 and was never combined with Gemma), but on test2 the guards alone let 3 of 40 critical and 1 of 300 adversarial sentences through; the guards are only as good as their word lists.
* **Only the form "Gemma AND baseline gate" showed no observed unsafe credit** (0/40 and 0/300 on all three splits) and it credits fewer legitimate sentences than the baseline alone (73, 60 and 57 of 140); it is a research candidate at most. Synthetic builder-written labels, no independent review (EX-01), a baseline tuned on these splits, test2 previously used by the Qwen experiment, 7 to 21 percent fenced (strict-malformed) replies, cloud latency and privacy open. `speech_interpretation` stays OFF; no audio, Quest or clinical claim.

## 0k. Update of 20 October 2026 (unsafe-speech work: review readiness, no new result)

* **Prepared, not performed:** a blind review kit (packaging, check, agreement, adjudication, merge into an override file), the protocol and its readiness table (`docs/testing/safety_challenge_review.md`), an audit of the sets (every label provisional; none independently reviewed), a design for the audio-to-transcript evaluation (`docs/testing/asr_safety_evaluation.md`, not run, not validated).
* **Frozen:** the candidate, the sets and the final results are pinned (`tests/safety_challenge/frozen.json`); the ten final-split failures (DT-233) are explained as four closed-list or window mechanisms and left unfixed; a fix needs a development set and a fresh final set.
* **Tests:** mutation coverage now includes the route fallbacks and audit rules against PostgreSQL and the evidence store end to end through the session log (49 mutants); the Unity client is not mutation-run.
* **Open:** reviewers and an adjudicator (EX-01), the independent run, the audio evaluation (DT-235), the Quest checks (DC-08, DC-09, NOT RUN); `speech_interpretation` stays OFF.

## 0j. Update of 20 October 2026 (unsafe-speech hardening: measure, refuse by named layer, mutation-test)

* **Audit and taxonomy:** every safety layer was read against the code (`docs/research/safety_layers_audit.md`); unsafe speech was classed into categories with the expected decision, the safeguards, the residual risk and the tests (`tests/safety_challenge/taxonomy.json`). The recall tests missed a disabled `validate` because overlapping guards mask each other.
* **Measured on 2,079 metamorphic rows** (seed, template and expected outcome on every row; split by seed family and template; labels by construction, PROVISIONAL, ambiguous or clinically relevant rows PENDING_HUMAN_REVIEW): the unchanged `4a2ed00` let 15 to 17 percent of must-not-credit rows through to the engine's evidence store on development and challenge. Changes: wider assertion kinds, the engine checks the waiting question and read-back itself, reason-coded audit rows, mutation-tested guards; cap and thresholds unchanged.
* **Result, synthetic and preliminary:** final split scored once per configuration with no change after seeing it: 41/407 to 10/407 must-not-credit rows credited, legitimate credit unchanged. Not zero. Development and challenge zeros are not generalisation (used for changes). No person has reviewed any row; independent validation (EX-01, DT-233, DT-234) and headset checks (DC-08, DC-09, NOT RUN, Sannan) are open; `speech_interpretation` stays OFF.

## 0i. Update of 20 October 2026 (legitimate speech recall: diagnosis first)

* **Traced, not assumed:** every missed legitimate row went through every stage (`docs/research/recall_diagnosis.md`). The critical-mass cap is the primary cause of 58 to 76 percent of the misses, but it blocks nine unsafe sentences when removed, so it was raised only to the largest value the safety panel supports (0.03 to 0.05, margin 1.5 times). A second cause was downstream: the engine's evidence store used a placeholder 0.8 threshold and refused evidence the gate had credited; it now holds the classifier's calibrated thresholds.
* **Assertion layer:** hand-written counterexamples exposed holes (other speakers outside a short role list, delay markers, conditionals); closed, with 84 of 84 counterexamples now named by the layer itself and no legitimate sentence newly flagged.
* **Result, synthetic and preliminary:** evidence accepted by the engine for the right act 63 to 76 of 140 (test) and 67 to 79 of 140 (test2) on the held-out splits scored once, no critical, non-assertive, out-of-window or panel sentence accepted. The independent set (EX-01, DT-231) must judge the revised cap before any claim; `speech_interpretation` stays OFF; device checks are NOT RUN (Sannan).

## 0h. Update of 19 October 2026 (how trainee speech becomes evidence: ADR-0022)

* **Decided:** transcript + the real scenario state -> candidate acts -> deterministic validation -> the engine. The engine builds the context (running steps, the objectives eligible at them from the behaviour windows of `content/assessment/mapping.json`, a pending patient question, a pending read-back, recent turns, acts already accepted); the retained classifier gate (ADR-0020) proposes only eligible acts; rules check status, critical content, evidence and duplicates; the answer is evidence, one clarification, abstention or the menu; `EvidenceStore` in the engine accepts or refuses. An act outside its window is not eligible (the engine's existing rule).
* **Measured the same day, synthetic only (tests/action_bench_v1, not independent):** zero-shot NLI (BART-large-MNLI) core accuracy 0.26 and 0.39 against 0.80 and 0.87 for the head (`docs/research/nli_zero_shot.md`); a Qwen 4B rubric evaluator over all objectives credited 6/40 critical sentences on test2 even with the production guards and needs 3.4 s median, 4.1 s p95 against 0.79 s left in the voice round trip (`docs/research/rubric_evaluator.md`). Neither is in the live path. That evaluator had no real per-row context: context conditioning is untested, not refuted.
* **Built and tested the same day (engineering complete; headset and independent evidence outstanding):** p1-ai-language-s10 to s14 (DT-226 to DT-230): the engine context and speech evidence, the `/interpret` service, clarification and menu with the Unity script, dialogue noise guard, the benchmark. On the synthetic benchmark legitimate recall is unchanged (66 of 140 test, 69 of 140 test2) (superseded on 20 October 2026: section 0i) and every context rule only removes credit (`docs/research/interpretation_benchmark.md`). New gate `speech_interpretation`, OFF; its thresholds are proposed and wait for the owner (DT-229); it can pass only on the independent set (EX-01). Headset check DC-09 is Sannan's.

## 0. Planning update of 12 October 2026 (read this first)

This audit is now the short summary. The full, authoritative planning set is in [architecture/](architecture/README.md):

* **Every AI feature, one entry each (68: A1 to E65 except one retired number, plus A66, D67, D68, E69)** with status, the AI part's status, evidence and a complete implementation plan per feature: [feature_registry.md](architecture/feature_registry.md), [plans/features/](plans/features/) (pointer: [architecture/feature_plans/](architecture/feature_plans/README.md)), [coverage audit](architecture/feature_coverage_audit.md). Status counts: 3 implemented and tested, 28 partially implemented, 14 planned, 3 blocked, 3 deferred, 17 proposed. The two features rejected in the first pass (generative images, LLM fine-tuning) were reassessed and kept as deferred research tracks. 104 capability plans, 64 of them AI, each with a baseline it must beat.
* **AI composer and personalisation:** the scenario composer is an LLM with retrieval over a scenario knowledge base, enforced by the solver and validators; a Bayesian learner model and a targeting policy shape each next case: [ai_scenario_generation_architecture.md](architecture/ai_scenario_generation_architecture.md).
* **Fresh scenarios every session:** a 21-stage live generation pipeline (LG-01 to LG-21) that uses the cases and datasets as templates, evidence and evaluation material: [live_generation_spec.md](architecture/live_generation_spec.md). The first fresh scenario needs **no language model**: dataset profiles, parameter packs, blocks, the solver, statistical numbers, tagged images, validation and packaging.
* **Roadmap:** 24 phases (PH-01 to PH-24) with a critical path that avoids the current blockers: [master_roadmap.md](architecture/master_roadmap.md). New plan modules: `p1-dataset-analysis`, `p1-live-scenario-generation`, `p2-ai-learning-analytics`, `p2-ai-advanced-simulation`.
* **Policy changes** (ADR-0008, INSTRUCTIONS 8i revised and 8j): clinician sign-off is no longer a development gate; licensing is checked and recorded, not a blanket blocker; served or pretrained models are preferred over training; datasets are analysed before generators are designed; rules still decide (ADR-0005).
* **Canonical graph:** `architecture/procsense_full_pipeline.json`, checked by `tools/plans/validate_pipeline.py` in CI. It is also exported as 10 Synverk flow diagrams in `architecture/flows/` (start with `00-live-pipeline.json`).

Nothing in the planning update was implemented; sections 1 to 6 below are the audit as written earlier the same day, with blockers revised where the policy changed.

## 0b. Update of 16 October 2026 (local models installed, first measurements)

Done with the owner's approval; **no AI feature is built on these models yet (no code calls a model)**. Decision and reasons: [ADR-0010](decisions/ADR-0010-local-models-qwen35-live-and-batch.md).

* **Installed on the desktop (GTX 1660 SUPER):** Ollama 0.40.5; `qwen3.5:4b-q4_K_M` (live work, thinking off), `qwen3:4b` (comparison baseline only). The laptop is not set up.
* **Measured on the idle desktop** (`tools/ai/benchmark.py`, 4096 context, schema output, one machine and one prompt shape): the 4B is 100 percent on the GPU, about 59 tokens per second, p50 1.01 s and p95 1.15 s for a 50-token patient reply; the 9B is 57 percent on the GPU, about 12 tokens per second, about 5 s for the same reply, so it is batch only. Structured output was valid 100 of 100 (4B) and 40 of 40 (9B). Speech recognition and the Unity Editor were not loaded.
* **What the numbers do not show:** quality. The validator reads text only; reading the replies found a few made-up denials ("I don't smoke") that it cannot catch (DT-190). A blind human rating is pending (DT-187).
* **Still to do:** the laptop, co-residency with speech recognition and Unity, thinking-on runs, debrief, composition and judge tests (DT-188).
* **Harness gap closed:** the patient now has an approved "cannot recall" intent (DT-189).

## 0c. Update of 17 October 2026 (build started, by the owner's instruction)

The owner told the project to implement the P1 and P2 plan modules that are the engineer's, in order. Status as the code and tests show it (each item has a check in CI):

* **Dataset analysis (PH-02, PH-03): built.** Numeric and waveform profiles, image tags and technical quality grades, generator packs with a sampler, evaluation sets: `tools/data/profile_*.py`, `tools/content/imaging_tags.py`, `tools/generation/`, `content/generation/packs/`, `tests/generation/`.
* **Scenario knowledge base (A66, OFF-10): partly built.** 836 sourced relations and an offline extraction tool with quote checking and a review gate (`tools/kb/`, `content/kb/`). The extraction precision gate (0.8 on 100 hand-labelled relations) is not measured: it needs a labeller (DT-114). No model relation is in the base.
* **Still true:** no live session calls a model; rules decide, AI proposes.

## 0e. Update of 18 October 2026 (targets, safety, the AI-judged classifier evaluation)

Built and measured by the engineer under the owner's instruction (details and numbers in `docs/report/ai-eval.md`, the step notes and `docs/plans/task-reconciliation.md`, which states for each of the 97 P1 and P2 steps what exists and what is open):

* **Safety:** the side of the body and denied pain are now checked by plain code on every generated package, with no model (`server/generation/consistency.py`; 36 of 36 denied pains and 28 of 29 flipped sides on the seeded set, no flag on any authored case); critical sentences are never credited from speech and, with the new guards, none of the evasive critical sentences written for the check is credited under an ordinary act.
* **Targets that were unmet and now pass:** live speech 2.19 s (target 2.5); AI debrief 93.8 percent accepted, 89.6 percent within 8 s on 48 sessions; plan composer 20 of 20 served at coverage 0.877 against 0.746; performance interpreter 84.2 against 73.7 percent for random picks (simulated learners); text critic +62.7 points over the rules on a held-out set, false alarms 3.8 percent; three characters 3.77 s (target 4) with the two staff choosing approved lines in one call. Each was measured before and after, with the earlier numbers kept in the result files.
* **The classifier:** evaluated on two AI-judged synthetic sets (model-written, labelled by two configurations of one local model, contested rows settled by the same builder: **not independent, not validation**). TF-IDF equals or beats the embedding head on both (0.816 and 0.792 on the v2 test scored once; 0.892 and 0.842 on a fresh hold-out); Qwen is far behind (0.654). A retrained head, a critical-mass cap and a longer cue list took the critical sentences credited under an ordinary act from 5 to 0 (v2 test, post hoc) and to 0 on the fresh hold-out. It stays off.
* **Core and cases:** a patient twin (`src/ProcSense.Core/Twin`, 7 tests), wrong-path goldens for the 16 P1 cases (142 goldens, engine 0.0.9), LOINC and ICD-11 code tables with a check in CI.
* **Analysed, not built, with the reason:** Pulse (no compiler, no CMake, 23 GB free: DT-207), generative images (no licensed data holds the missing findings), model adaptation (cheaper remedies work), MedGemma (the owner must accept the terms), on-device speech (the Quest), drug availability (the registry forbids citation).

**The gate now switches on:** asr, critic, debrief_ai, interpreter, multi_agent, narrative, patient_dialogue, plan_composer, tts, voice_orders. **Off:** classifier (SKIPPED: the independent set does not exist), classifier_synthetic and classifier_synthetic2 (recorded for information; the second's independent-evidence suite cannot pass by design).

## 0g. Update of 18 October 2026, third session (classifier safety, 4B instead of 9B, handoff, matrix)

Every number is in the file named beside it; synthetic and AI-judged material is labelled and is not validation.

* **Classifier:** the 3 of 48 false credits are fixed (0/48) by a two-head agreement rule, hard negatives and an assertion check; a frozen challenge set (scored once) went from 5/60 to 0/60; permanent regression suite in CI; the classifier stays OFF (`docs/research/classifier_benchmark/safety_gate.md`, ADR-0016, EX-01).
* **Models:** the 4B replaces the 9B in every production path (`docs/research/model_4b_vs_9b.md`, ADR-0017). Qwen is not a classifier (macro-F1 0.59 and 0.80 on the same sentences against 0.93 for the head).
* **Reconciliation:** all 269 steps (`docs/plans/task-reconciliation.md`); the single outside-testing page is `docs/testing/SANNAN_EXTERNAL_TESTING.md` (37 items).
* **Features:** `docs/architecture/feature_implementation_matrix.md`: of 68 plans 41 are complete, 16 engineering complete with an external acceptance pending, 5 partly built (AI capabilities no step built or below their acceptance, DT-224), 3 built and gated off, 1 owned by others, 2 research with the trigger not met. The pipeline JSON statuses follow the reconciliation.
* **Built:** the multi-problem composition engine (DT-220: authored values missing), expected-gain targeting as the practice default with Elo as fallback (`docs/research/personalisation_study.json`), the spoken-order route with signed tickets and a model fallback that is gated OFF (70.5 percent against the plan's 90 to 95).
* **Gate:** on: asr, critic, debrief_ai, interpreter, multi_agent, narrative, patient_dialogue, plan_composer, tts, voice_orders. Off: classifier (no independent set), order_fallback (below its acceptance), and the informational classifier_safety, classifier_synthetic, classifier_synthetic2.

## 0f. Update of 18 October 2026, second session (CI without Actions, one language feature retired, Pulse built, speech on the card, fresh adversarial sets)

Measured or built by the engineer under the owner's instructions; every number is in the file named beside it. Synthetic and AI-judged material is labelled as such everywhere and is not validation.

* **CI:** GitHub Actions cannot start jobs (the account's payments failed or its spending limit was reached: no check of the code ran remotely for the pushes after `e517f67`). Nothing was bought. `task ci` is the required validation, `tools/ci/remote_status.py` keeps local and remote results apart, a run that never started is never a pass (docs/CI.md).
* **Scope:** The retired language feature (owner decision, Change Report 117) is removed (feature, step, plan and deferred item; 68 feature plans remain; Change Report 117). The product is English only.
* **Pulse:** built and run on this desktop with free tools (recipe and fixes in `tools/pulse/README.md`); four scenarios recorded; the adapter replays a recorded run behind `IPhysiologyEngine` (11 C# tests, all 142 golden runs with a trace selected); LitePhysio stays the default (9 of 27 comparisons agree in direction; the main effect of each action does). Quest cost: Sannan, DC-05.
* **Speech:** the recogniser runs on the card beside the resident 4B model: 0.58 s for a 3 to 7 s clip (target 0.7; 5.9 s on the processor). On 257 utterances of real mock consultations (PriMock57, CC BY 4.0, actors, British accents) the word error rate is 0.217 (doctors 0.234, patients 0.183). On-device candidates on the same clips: Whisper tiny.en 0.203, a 20M Zipformer 0.49 (ADR-0014, proposed; headset: DC-04).
* **Classifier:** still off, and the picture is less rosy than the numbers of 17 October: on 48 FRESH evasive critical sentences the full path credits 3 under an ordinary act and the cue list alone catches 6; the cues are a floor, not the protection (`reports/ai/classifier-guard-only.json`, ADR-0013). A different-size labeller (qwen3.5:9b, J3) cross-checked the v3 hold-out: it agrees with the final label on 0.80 of 300 sentences (0.85 with J1, 0.80 with J2); 59 disagreements, 14 involving a critical act, listed in `docs/research/classifier_benchmark/cross_judge_disagreements.md`; on the v2 test split (600 sentences) it agrees with the final label on 0.822 (107 disagreements, 24 involving a critical act). The v2 dev split was not cross-checked.
* **Dialogue:** 90 seeded stress lines (normal, abnormal, ambiguous, contradictory, adversarial): 90 pass the reply rules after the leak rule was refined once (SD-012), median 1.86 s, p95 2.39 s.
* **Gates and triggers:** `tools/ai/adaptation_trigger.py` (CI) says neither the language-model nor the picture adaptation trigger is met; MedGemma is gated and unaccepted (`content/ai/model_candidates.json`); drug availability sits behind providers with live off (`docs/research/drug_availability.md`).
* **Steps:** 48 steps ticked because their own criteria are met; 23 are "engineering done, external acceptance pending" with who is needed; the page is `docs/plans/task-reconciliation.md`; headset tests: `docs/plans/sannan-device-checks.md`.

## 0d. Update of 17 October 2026 (the AI features are built; what the gate says)

Built by the engineer under the owner's instruction, each with tests in CI and a measured result in `reports/ai/` (the generated page is `docs/report/ai-eval.md`):

* **Model layer:** Ollama client, hashed prompt registry, gateway with schema, validator, one retry and the fallback signal, audit, rate limit and one model slot (ADR-0011).
* **Generation with models:** patient wording, the AI plan composer (valid but did not beat the solver: off by default), the text critic with a verification pass, number-locked reports, the joint model of laboratory results.
* **Characters and speech:** patient and staff dialogue (only the facts the Core allows are shown), the AI debrief (template first, strict validator), speech recognition (`faster-whisper`, CPU here), live speech (Piper; a Windows fix for standard output), spoken orders with read-back.
* **The classifier:** sentence embeddings + logistic regression with a critical-content guard (ADR-0012); SBAR_ELEMENT made critical in the engine. Evaluated only on a synthetic, model-written, model-labelled set of eight styles; **not independent**.
* **Learning and personalisation:** session evidence, learner model, targeting policies (neither beat the Elo selection in simulation), explanations, patterns, calibration, reasoning features, progression, next-case ranking, result explanations.
* **Safety work:** red team of the validator (critical classes zero escapes; paraphrase, synonym and made-up denials are open, DT-211).
* **Theatre extension and cases:** Sign Out, four labelled intra-operative events, the recovery handover, 16 P1 cases with four goldens each (correct, novice, dangerous, edge; 142 goldens in all, engine 0.0.9).
* **Measured and not shipped:** retrieval over approved content (DT-216); the AI plan composer; personalised targeting.

**The gate (`content/ai/features.json`) switches on:** asr, narrative, patient_dialogue, voice_orders. **Off:** classifier (SKIPPED), classifier_synthetic (FAIL), critic (FAIL), debrief_ai (FAIL), interpreter (FAIL), plan_composer (FAIL), tts (FAIL). The classifier is off because its suites read the independent set (DT-210), the critic and others because their result does not yet meet the bar (see the page). A missing result never passes.

**Needs a person:** the independent classifier set (two authors per split, a second labeller, an adjudicator), consented recordings (the draft rules are in `docs/research/labelling/consent_and_storage.md`), a clinician for the intra-operative events and the 12 recovery items, the laptop, and Sannan's scenes, assets and headset runs. Pulse, on-device speech, the medical model benchmark and the content extensions wait for their own needs (DT-207, DT-212).

## 1. The short answers

| Question | Answer |
|---|---|
| Does P0 contain any trained model or LLM? | **No.** P0 is deterministic rules and tables only. Nothing in P0 is called AI, and no rule is dressed up as a model. |
| Where does AI start? | **P1** (milestones M10 to M12): a locally served language model (Ollama on a 6 GB GPU: the desktop GTX 1660 SUPER or the laptop RTX 4050, chosen by the dual-GPU benchmark in `p1-ai-infrastructure-s02`) for patient and staff lines, a communication classifier and a debrief rewrite; speech recognition on the desktop GPU; a FastAPI backend. |
| Where are models *trained*? | **Only in P2** (`p2-ai-extensions-s03`): a small classifier trained on the human-labelled set. The P1 classifier, dialogue and debrief use a served model with prompts, **not** training. Speech recognition is an off-the-shelf model, not trained here. |
| Who decides what is correct? | **Always the rules.** An AI only (a) turns a trainee's words into a structured act for the evidence mapper, (b) voices a line the rules have already decided, or (c) rewrites the template debrief from facts. It never sets state, never credits a critical item and never reaches the trainee unchecked (ADR-0005). |
| What is built today? | The whole **deterministic foundation** that the AI stages stand on, plus the **safety gate** that makes AI output safe to show. See section 3. |
| What is blocked? | Everything that needs a model, a GPU, human labellers, consented recordings or the backend. See section 5. Nothing was faked to look finished. |

## 2. Every AI idea, one row each

Status words: **Built** (code and tests exist), **Built, rules only** (works, but it is rules or a rating, not a model, and is labelled so), **Planned** (a step file exists, nothing built), **Blocked** (cannot be built without something named in section 5).

| Idea from the plans | Plan step | Stage | Status | What exists | What is missing |
|---|---|---|---|---|---|
| Rules, scoring and evidence that AI evidence plugs into | `p0-scoring-debrief` s01 to s05 | P0 | **Built, rules only** | 22 assessed behaviours, a rubric, `Scorer`, `EvidenceStore` (critical guard, thresholds, context check), outcome and fail gate, `DebriefFacts`, template debrief, `report` command | Owner approval of the steps; clinician review of weights (Q8) |
| Analysis of trainee performance | same | P0 | **Built, rules only** | Per-behaviour credits and penalties, lateness, a timeline with log references, one JSON and one HTML report | Nothing in P0; richer analysis is the AI debrief (below) |
| Difficulty a trainee has (what they find hard) | `p0-case-library` s06 | P0 | **Built, rules only** | Elo-style rating per behaviour (K 16, 3 sessions before adapting) from the scorer's credits and penalties | A clinician's review of the dimension mapping (Q9) |
| Progressively harder, personalised scenarios (selection) | `p0-case-library` s06 | P0 | **Built, rules only** | 8 difficulty dimensions, case tags, `Selector` (approved pool only, practice only, fixed order in assessment), `next-case` and `record-session` commands, pseudonym history | It chooses among the 53 existing cases; making new ones is the composer |
| Beginner versus advanced behaviour | `p0-case-library` s06 | P0 | **Built** | `SessionOptions.Level`: the nurse challenges (beginner) or the consequence plays out (advanced); saved in the log header | none |
| Validation of any case, hand-made or AI-drafted | `p0-case-library` s04, s05 | P0 | **Built** | Stage B: 11 ordered checks, 12 contradiction types, HTML report per case, 60 of 60 deliberately broken cases rejected, an AI draft can never reach an approved status | Checks 9 and 10 need a golden run per case (17 P0 cases have one) |
| Unexpected events | `p0-physiology-events-behaviour` s08, s09 | P0 | **Built** | `EventDirector` (seeded draw in practice, fixed schedule in assessment), 7 approved events, what-if coverage and bindings | 7 what-ifs have no state to run on (DT-087); events are approved for training only (Q7) |
| **AI-generated clinical scenarios and problems** | `p1-scenario-composer-difficulty` s01 to s05 | P1 | **Planned** | Only the gate they must pass (Stage B) and the context they need (case tags, discovery rules) | Block library with requires/excludes, the deterministic solver, the LLM proposal step (clinician review and approval of composed cases were required here; since 12 Oct 2026 they are not a gate: generated cases are practice-only and gated by automated validation, ADR-0008). The solver is **not AI** and comes first; the LLM only proposes block combinations and a narrative |
| **Grounding validator** (no AI line reaches a trainee unchecked) | `p1-ai-language` s01 | P1 | **Built** | `server/validation/validator.py` and its C# twin, 44 shared vectors, 100+ disclosure probes, every lexicon drug rejected when off-card, debrief mutation tests | The FastAPI server around it (`p1-backend-s01`) |
| **Read-only context for AI characters** and who speaks next | `p1-ai-language` s03 | P1 | **Built** | `ContextBuilder` per role, hidden facts never in what the model is sent, 900-token budget, `ReactionQueue` (priority, debounce, one spontaneous remark per 8 s) | The Unity assembly split (`AIClient` asmdef) is Unity work |
| LLM serving benchmark | `p1-ai-language` s02 | P1 | **Partly built** | Ollama 0.40.5, `qwen3.5:4b-q4_K_M`, `qwen3.5:9b-q4_K_M` and `qwen3:4b` on the desktop; `tools/ai/benchmark.py` (speed and GPU share, structured output, grounding, blind rating sheet) with 13 tests; first measurements on the idle desktop (16 Oct 2026) | The laptop RTX 4050; co-residency with speech recognition and Unity; thinking-on runs; the blind human rating (DT-187); debrief, composition and judge tests (DT-188); model licences for MedGemma if the medical benchmark is run |
| **Patient dialogue** (free questions answered in the patient's voice) | `p1-ai-language` s04 | P1 | **Planned, blocked** | The rules that decide disclosure (history step, behaviour gate), the context, the validator, the approved quoted answers | A served model; the persona tags in the case files (DT-085); the adversarial question set |
| **Staff AI** (nurse, anaesthetist, porter, lab) | `p1-ai-language` s05 | P1 | **Planned, blocked** | Approved intents per role (`content/ai/intents.json`), the existing fixed lines | A served model; pre-rendered fallback lines per intent; voices |
| **Communication taxonomy and labelled set** | `p1-ai-language` s06 | P1 | **Blocked** | The 22 behaviours and their AI-reportable acts (`content/assessment/mapping.json`) are the start of the taxonomy | At least 300 utterances labelled by two people (kappa 0.6 or more), consented role-play recordings, a clinician's list of critical acts |
| **Communication classification** | `p1-ai-language` s07 | P1 | **Blocked** | The evidence mapper already records an AI act but never credits a critical act, a low-confidence act or a wrong-stage act | The labelled set, a served model, threshold calibration. Below macro-F1 0.75 it stays switched off |
| **Speech recognition** | `p1-voice-input` s01 | P1 | **Blocked** | none | The desktop GPU, faster-whisper, a vocabulary built from content, accented-speech tests |
| Voice orders and live speech output | `p1-voice-input` s03, s04 | P1 | **Planned, blocked** | The text verbs and the lines already exist | ASR, a voice client in Unity, a TTS voice |
| **AI debriefing** | `p1-ai-language` s08 | P1 | **Planned, blocked** | The template debrief (always shown first), `DebriefFacts`, the validator that rejects a dropped fact, a changed number, a flipped verdict or an invented event | A served model; 50-session mutation run |
| **Adaptive feedback** | `p0-scoring-debrief` s04 and `p1-ai-language` s08 | P0 / P1 | Template feedback **built, rules only**; AI wording **Planned** | The template sentences are chosen by item, never invented | The AI rewrite |
| AI evaluation suite and the switch-off gate | `p1-ai-language` s09, `p1-evaluation` | P1 | **Planned** | The pieces it will run (validator tests, mutation tests) | The harness, the weekly schedule, `ai_features.yaml` |
| Backend (sessions, upload, reports, audit, AI gateway) | `p1-backend` s01 to s06 | P1 | **Planned** | The report format the backend reuses (`debrief_facts.v1`) | FastAPI, storage, a login decision |
| On-device speech, retrieval, medical LLM benchmark | `p2-ai-extensions` s01, s02, s05 | P2 | **Planned** | none | P1 first |
| **A trained model** | `p2-ai-extensions` s03 | P2 | **Planned, blocked** | none | The human-labelled set from P1 s06; training code; the frozen test split |
| PULSE physiology engine | `p1-pulse-physiology` | P1 | **Planned** (not AI) | LitePhysio is the P0 engine | The spike and the parity experiment |

## 3. What this audit built (all with tests; none of it is a model)

| Piece | Where | Tests |
|---|---|---|
| Scoring, evidence mapper, outcome, debrief facts, report | `src/ProcSense.Core/Scoring/`, `content/scoring`, `content/assessment`, `content/debrief` | `ScoringTests`, `tools/ci/check_report.py` |
| Adaptive difficulty (rating and lookup) | `src/ProcSense.Core/Difficulty/`, `content/difficulty` | `DifficultyTests` |
| Stage B validator and mutation tests | `tools/composer/`, `content/contradictions.json` | `test_composer.py` (60 of 60 mutations rejected) |
| Grounding validator, Python and C# | `server/validation/`, `src/ProcSense.Core/AIContext/Validator.cs`, `content/ai/lexicon.json`, `tests/vectors/grounding_vectors.json` | `test_validator.py`, `GroundingValidatorTests` |
| Per-character context, reaction queue | `src/ProcSense.Core/AIContext/ContextBuilder.cs`, `content/ai/intents.json` | `ContextBuilderTests` |
| Event Director and what-if coverage | `src/ProcSense.Core/Events/`, `content/events` | `EventDirectorTests`, `WhatIfBindingTests` |
| Seventeen correct-path runs of the P0 cases | `tests/golden/*_correct.*`, `tools/golden/p0_scripts.py` | `P0CaseTests` |

## 4. How to read "AI" in this project

1. **Rules** decide correctness. They are written down, sourced and tested. Most of the engine is this.
2. **A rating or a lookup** (difficulty selection, case tags) is arithmetic over the scorer's output. It adapts, but it is not a model and is labelled "a rating and a lookup, not a trained model" wherever it appears.
3. **A served language model with a prompt** (P1 dialogue, staff lines, classifier, debrief rewrite, composer proposals) interprets or phrases. It is not trained here. Its output passes the grounding validator or it is replaced by a template or a pre-rendered line.
4. **A trained model** exists nowhere yet. The first one is the P2 small classifier, trained on a human-labelled set, with a frozen test split and the critical-act guard.

Known limits of the checks that are built:
* The disclosure check is lexical: it catches a hidden fact told in its own words or numbers, not a paraphrase. The primary protection is that a model is never given a hidden fact (the context does not contain it); the validator is the second line. A shared vector documents the limit.
* The lexicon lists are curated (SR-2870) and must grow when a model slips a synonym through.
* The classifier thresholds, the 0.8 default and every scoring weight are design values awaiting the clinician (Q8, Q9, Q13).

## 5. What blocks the rest (exact needs)

| Need | Unblocks | Who |
|---|---|---|
| (task DT-113) ~~Ollama installed on the desktop~~ done 16 Oct 2026; still needed: Ollama and the models on the laptop RTX 4050 (6 GB), and MedGemma terms accepted only if the medical benchmark is run | `p1-ai-language` s02, s04, s05, s07, s08, composer s03 | Owner |
| The desktop with the GTX 1660 Super and faster-whisper | `p1-voice-input` s01 to s04 | Owner |
| Two people to label at least 300 utterances, consented role-play recordings, a clinician's list of critical acts | `p1-ai-language` s06, s07, and the P2 trained classifier | Owner, a clinician |
| A decision on the backend hosting and login | `p1-backend` s01 to s06 | Owner |
| ~~A clinician to review the 21 block types~~ no longer a gate (12 Oct 2026, ADR-0008); the block library itself is the need | `p1-scenario-composer-difficulty` s01 to s05 | Case authors |
| The persona tags in the case files (each case edit resets its approval) | patient dialogue style (DT-085) | Case authors |

When any of these arrives, the next step is already planned and the gate it must pass already exists.

## 6. Order the stages unlock in

`P0 scoring and evidence` (built) → `grounding validator and context` (built) → `backend skeleton (s01)` → `LLM serving benchmark (s02)` → `dialogue and staff AI (s04, s05)` and `debrief rewrite (s08)` → `labelled set (s06)` → `classifier (s07)` → `evaluation harness and switch-off gate (s09)` → `P2 small classifier (the first trained model)`.

Live generation runs alongside this order and does not wait for it: `dataset analysis` → `parameter packs and media tags` → `block library and solver` → `skeleton` → `numbers and media` → `validation, repair, novelty` → `package and pool` (see the master roadmap).

Decision records: `docs/decisions/ADR-0005-ai-boundaries-and-staging.md`, `docs/decisions/ADR-0008-live-generation-and-planning-policies.md`. Open items: `docs/deferred-tasks.md` DT-096 to DT-112. Clinician questions (research items, not gates since 12 Oct 2026): `docs/research/clinician_signoff.md` Q8, Q9, Q13.
