# p1-ai-language-s19 — Failure attribution of the semantic reader, and the corroborated cap behind a setting (implemented OFF; the live panel check failed)

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
Owner requests of 21 October 2026 (prompts 179 and 180): explain why the earlier guarded Gemma evaluator credited 118 of the 140 legitimate dev rows and the live production reader 92, then (after the proposal was approved) implement the smallest safe change, a corroborated critical-mass cap behind `cap_policy`, test it, and run one bounded live panel check.

1. **Analysis** (`tools/bench/failure_attribution.py`, `docs/research/failure_attribution.md`, `reports/ai/failure-attribution-dev.json`, `docs/research/semantic_reader_architecture_proposal.md`): 118 to 92 is 34 rows lost and 8 gained; 27 of the 34 lost to the head's critical-mass cap, 4 to the engine threshold, 2 to timeouts, 1 to the critical-act menu; in 30 of the 34 the model's reading was right.
2. **Stage one** (`server/interpret/semantic.py`): a strict-valid reading that reports ANY critical objective, under any verdict, sends the sentence to the menu. Always on when the reader runs; can only remove an offer; cost no legitimate row in the replay.
3. **Stage two** (`interpret_llm_cap_policy`, default `hard`; `corroborated()`): the hard cap is bypassed only if the reading is strict-valid, exactly one finding is SUPPORTED, no finding is critical, and the head's probability for that objective is at least the whole critical mass; every other check still applies. **OFF; not to be enabled.**
4. **Tests**: new classes in `server/tests/test_semantic_interpreter.py` (the six previously surviving raw false-credit patterns under both policies, recovered rows, every clause and boundary, threshold, window, waiting state, duplicates, non-performed, cue, invalid readings, every critical act, defaults, wiring), `server/tests/test_cap_policy_reports.py` (committed evidence); mutants SM9 to SM12, SM14 to SM18, SC2, SC3 (and SM2, SM5 updated): all 29 semantic mutants killed.
5. **Replay** (`tools/bench/corroborated_replay.py`, `reports/ai/corroborated-cap-replay.json`): corroborated against hard on dev (live and recorded), test, test2: recall 113/140 against 92 (live dev), 110 against 88, 95 against 75, 104 against 75, no row lost, credited precision 113/115, 110/112, 95/95, 104/104; 0 critical, 0 adversarial, 0 out-of-window and 0 no-context offers; the 67 historical raw false credits: 0 of 67 offered in any context, view or policy.
6. **Bounded live panel** (`tools/bench/cap_panel_live.py`, `tools/bench/cap_panel.json`, `reports/ai/cap-panel-live.json`): ONE run, `gemma4:31b` on the free plan, 12 calls, 17,988 tokens (ceiling 50,000), no retry, no billing signal. Of the 12 sentences that only the model's critical report can stop, **5 were offered under the corroborated policy, all 5 critical-content sentences of the panel** (SBAR_ELEMENT 2, HANDOVER_CRITICAL_ELEMENT 2, CONSENT_SIGN 1); the model's critical report stopped 4; 3 abstained. **The corroborated policy fails the panel and stays OFF.**
7. **Pending critical state** (decision 3): inspected, documented, NOT added to the context (`docs/research/corroborated_cap_results.md` section 4).

## Why
The 26-row recall gap needed a row-level explanation before any more models or thresholds; the approved smallest change recovers synthetic recall but, measured on the panel, reopens the very class of sentence the cap protects. The result is recorded so nobody enables the policy on replay numbers alone.

## How to implement
Already implemented. Repeat: `server/.venv/Scripts/python.exe -m pytest server/tests/test_semantic_interpreter.py server/tests/test_cap_policy_reports.py`; `server/.venv/Scripts/python.exe tools/bench/corroborated_replay.py --write` (needs the git-ignored recorded replies and cached embeddings); the live panel needs the owner's approval each time.

## Verification
`server/tests/test_semantic_interpreter.py`, `server/tests/test_semantic_reader.py`, `server/tests/test_semantic_eval_tool.py`, `server/tests/test_cap_policy_reports.py` (115 DB-free tests), `tools/ci/test_mutation_safety.py` (semantic mutants), `tools/bench/safety_frozen.py --check`, the planning checks and the full local CI.

## Files changed
`server/interpret/semantic.py`, `server/config.py`, `server/routers/interpret.py`, `server/.env.example`, `server/tests/test_semantic_interpreter.py`, `server/tests/test_cap_policy_reports.py`, `tools/ci/mutation_safety.py`, `tools/bench/failure_attribution.py`, `tools/bench/failure_attribution_report.py`, `tools/bench/corroborated_replay.py`, `tools/bench/cap_panel_live.py`, `tools/bench/cap_panel.json`, reports, ADR-0024 addendum, research pages, plans and registers.

## Status (21 October 2026)
Engineering done; **the corroborated policy failed the bounded live panel and must not be enabled**; `cap_policy` defaults to `hard`; `speech_interpretation` and `interpret_llm_enabled` stay OFF. The 85 percent recall target is NOT MET and unchanged (113/140 on the design data under the policy that must stay off; 92/140 under the shipped one). Independent validation (EX-01), the privacy decision (DT-248), an independent critical-content signal (DT-238), real audio and the headset checks (DC-08, DC-09) are NOT DONE.

## Stage two (owner request of 21 October 2026: the target becomes 90 percent recall, 95 percent precision, 0 critical, 0 adversarial, 99.5 percent schema)
8. **Baseline attribution** of the 48 hard-cap dev misses (31 cap, 6 engine threshold, 5 critical cue/menu, 3 window/missing context, 2 timeouts, 1 wrong contested act): the semantic reader's own errors are small; the ceiling is set by the independent checks.
9. **Engine-state context** (implemented): `patient_question_pending` and `readback_pending` stated to the reader as engine facts (context only, no eligibility). One bounded live re-check (24 rows, 25 calls, 34,321 tokens): 12 to 15 rows credited under the right act (AB-d0066, d0079, d0082), 0 wrong, the 4 historical timeouts now answer; estimated dev recall under the hard cap **95/140**, precision 95/96 (the shipped path alone also 95/96). Mutants SP1 and SP2; 31 semantic mutants killed.
10. **Normalisation** explored and rejected (an edit-distance rule changes tense and meaning: "promised" to "promise", "scarring" to "scar ring").
11. **Label conflict** (escalation and recommendation wording across dev, test, test2) documented as an EX-01 item; the cue was not touched.
12. **Isolated critical-claim verifier** (`server/interpret/llm/verifier.py`, not wired in; ONE live run, 77 calls, 43,261 tokens): critical detected 34/34, missed 0, legitimate cleared 35/42, flagged 7; the 5 failed panel sentences all flagged; would clear 19 of the 21 rows the corroborated cap recovers, estimating 114/140 if it gated a cap lift. **Not integrated, no cap lifted**; the prompt was informed by the panel failures and the sample is small. Details, the evolution table and the four-way classification of what remains: `docs/research/recall_improvement_stage2.md`.

## Dependencies
p1-ai-language-s18 (the reader, the live runs), s15 and s16 (the cap and the panel).

## Common issues
The replay numbers come from sentences that informed the policy and are exploratory. The panel sentences are builder-written; 12 is a small sample, but 5 of 12 critical-content sentences offered is a failure of the safety property, not a rate estimate. The positive controls never reached the model (refused earlier by the shipped path).

<!-- external-testing:start (generated by tools/plans/external_testing.py) -->
## External acceptance

This step has acceptance that only a person, device or account outside the engineering desk can give. The exact procedure, pass rule and evidence are in the single source of truth:

* [EX-01](../../../../testing/SANNAN_EXTERNAL_TESTING.md#ex-01): Independent evaluation set for the speech evaluator: authors, second labeller, adjudicator, then the single scored run (paused; a fresh held-out set may replace it)

<!-- external-testing:end -->
