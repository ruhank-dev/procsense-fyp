# p1-ai-language-s16 — Unsafe-speech hardening: a taxonomy, metamorphic evidence, mutation-tested guards and an independent-review protocol

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
A follow-up to ADR-0022 and its recall addendum (owner request of 20 October 2026, prompt 176): make the speech-interpretation path robust against a broad range of unsafe, misleading, non-assertive, ambiguous and out-of-context trainee speech, with measurable evidence of generalisation rather than more hand-written counterexamples. Recall improvements are preserved; the critical-mass cap (0.05) and every threshold are unchanged; `speech_interpretation` stays OFF.

1. **Audit** of every safety layer (`docs/research/safety_layers_audit.md`): responsibility, what each detects and how, duplication, gaps, bypassability, deterministic against model-dependent, and why the recall tests missed a disabled `validate` (overlapping guards: the gate's assertion layer refuses first, so `validate` never decided anything on those inputs).
2. **Taxonomy** (`tests/safety_challenge/taxonomy.json`): negation and its scope, hypothetical, future and promises, unfinished actions, other-person attribution, quotation and reports, retraction, questions and advice, hedges and false completion, fragments and contentless speech, duplicates, out-of-window and no-waiting-context, critical claims, service failures, disabled gates, physical credit from speech; each with the expected decision, safeguards, residual risk and tests. No clinical requirement is invented (approved definitions and rubric rule 10 only).
3. **Metamorphic evaluation** (`tools/bench/safety_seeds.py`, `metamorph.py`, `safety_eval.py`; `tests/safety_challenge`): 2,079 rows from 71 seed families (each in three wordings and three verb forms) and 98 transformation templates, split by seed family and template (development, challenge, final); each row records seed, template and expected outcome; labels are by construction and PROVISIONAL, every ambiguous or clinically relevant row is `PENDING_HUMAN_REVIEW`; near-copies of `tests/action_bench_v1` and of development rows are dropped; raw predictions, rejection reasons and the configuration (file hashes, version, cap, model, commit) are kept; the final split is scored once per configuration; the unchanged `4a2ed00` is scored on the identical rows. Counts what the engine's own `EvidenceStore` accepts.
4. **Implementation** (`server/classify/assertion.py`, `server/interpret/decide.py`, `server/routers/interpret.py`, `src/ProcSense.Core/Speech/SpeechEvidenceSubsystem.cs`): assertion kinds `quoted`, `retracted`, `meta_question`, `hedged`, `contentless` and wider `denied`, `intended`, `other_actor`, `reported`, `hypothetical`, `incomplete`, found by development failures and generalised after a first look at the challenge split; the engine now checks, in the session itself, that ANSWER_QUESTION has a waiting patient question and CLOSED_LOOP a waiting read-back; every decision is audited by reason code (no words) with the rule-set version; `safe_interpret` makes a failing classifier the menu and lets a cancellation propagate.
5. **Mutation tests** (`tools/ci/mutation_safety.py`, `safety_scenarios.py`, `test_mutation_safety.py`): each critical check is disabled in a copy of the code; the interpretation path's decision and the engine store's verdict must change; double mutants show what the engine does when every service copy of a barrier is gone (8 of 9 cap-only sentences and 34 of 84 non-performed counterexamples are accepted by the engine); engine mutants show a critical act and an out-of-window act are credited only when an engine check is removed too.
6. **Independent validation prepared, not claimed** (`docs/testing/safety_challenge_review.md`, `tests/safety_challenge/review_sheet.csv`, EX-01 part B): blind review sheet (560 rows, no system output), roles, disagreement resolution, frozen configuration, metrics, acceptance criteria (existing thresholds PENDING, additions PENDING with rationale).

## Why
The recall follow-up proved legitimate speech can be accepted without loosening the cap, but safety had been judged on hand-written counterexamples, and a single mutation had passed the recall tests. Without a taxonomy, generated variants and mutation checks it was not known which unsafe wordings reach the engine, nor which guard is the last line for which case.

## How to implement
Already implemented; to repeat: `tools/.venv/Scripts/python.exe tools/bench/metamorph.py --write`, then `safety_eval.py --split dev --name NAME [--impl DIR] --write` (and `challenge`), `--split final --final` ONCE per frozen configuration; `git archive 4a2ed00 server content | tar -x -C artifacts/baseline-4a2ed00` for the baseline; `assertion_diff.py --before 4a2ed00`; `safety_regression.py`; `safety_review_sheet.py`; `unsafe_speech_report.py`; `tools/ci/mutation_safety.py --engine --write`.

## Verification
`tools/ci/test_safety_hardening.py` (16 tests; registered): the 190 sentences the unchanged implementation credited are each named as not performed by the assertion layer, minimal pairs with the legitimate neighbour, reason codes, `safe_interpret`, content invariants, sets reproducible and disjoint, taxonomy complete. `tools/ci/test_mutation_safety.py` (registered; builds engine copies): 20 single, 6 double and 12 engine mutants. Engine: `InterpretationContextTests` (a waiting question or read-back is checked in the session). `tools/bench/assertion_diff.py --before 4a2ed00`: no legitimate sentence newly flagged. `tools/bench/interpretation_bench.py`: 95/140, 76/140, 79/140 unchanged. `tools/bench/unsafe_speech_report.py --check`.

## Files changed
`server/classify/assertion.py`, `server/interpret/decide.py`, `server/routers/interpret.py`, `server/tests/test_interpret_api.py`, `src/ProcSense.Core/Scoring/Evidence.cs` (`SubmitUtterance`), `src/ProcSense.Core/Speech/SpeechEvidenceSubsystem.cs`, `InterpretationContextBuilder.cs`, `src/ProcSense.Cli/Commands.cs` (`evidence-check` sessions), `tests/ProcSense.Core.Tests/InterpretationContextTests.cs`, `tools/bench/{safety_seeds,metamorph,safety_eval,safety_regression,safety_review_sheet,unsafe_speech_report,assertion_diff,engine_acceptance}.py`, `tools/ci/{mutation_safety,safety_scenarios,test_mutation_safety,test_safety_hardening,run_all}.py`, `tests/safety_challenge/*`, `reports/ai/safety/*`, `docs/research/{safety_layers_audit,unsafe_speech_safety}.md`, `docs/testing/safety_challenge_review.md`, `docs/decisions/ADR-0023-unsafe-speech-hardening.md`, `docs/CI.md`.

## Status (20 October 2026)
Built and tested (implementation); measured on synthetic data (preliminary, builder-written, provisional labels). On the final split, scored once for each configuration with no change made after seeing it: unsafe credit 41/407 for the unchanged `4a2ed00` against 10/407 for the candidate (10.1 percent against 2.5 percent, 95 percent CI 1.3 to 4.5), legitimate credit unchanged (36/65). On development (126/828 to 0) and challenge (64/385 to 0) the code was changed against those rows, so they are not generalisation evidence. **Independent validation: NOT DONE** (no person has labelled any row; EX-01). The 10 remaining final-split failures are not fixed on purpose (DT-233). **Quest and headset checks: NOT RUN** (Sannan, DC-08 and DC-09). The `speech_interpretation` gate stays OFF.

## Dependencies
p1-ai-language-s10, s11, s14, s15 (the path, its benchmark and the recall follow-up), p1-ai-language-s07 (the gate).

## Common issues
A mutation that "survives" usually means another layer refuses the same input: test the layer on its own and remove all the copies of the barrier together. A result file for the final split is locked: a second look makes it development data. Never edit the generator to fit a reviewer's label; add an override file with the reviewers' ids.

<!-- external-testing:start (generated by tools/plans/external_testing.py) -->
## External acceptance

This step has acceptance that only a person, device or account outside the engineering desk can give. The exact procedure, pass rule and evidence are in the single source of truth:

* [DC-09](../../../../testing/SANNAN_EXTERNAL_TESTING.md#dc-09): Spoken communication evaluated against the scenario state: graded communication credit, menu fallback and no credit for critical or non-performed speech (English only)
* [EX-01](../../../../testing/SANNAN_EXTERNAL_TESTING.md#ex-01): Independent evaluation set for the speech evaluator: authors, second labeller, adjudicator, then the single scored run (paused; a fresh held-out set may replace it)

<!-- external-testing:end -->
