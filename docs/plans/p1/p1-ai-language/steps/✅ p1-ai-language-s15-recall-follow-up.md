# p1-ai-language-s15 — Legitimate speech recall: diagnosis, the engine's evidence thresholds, the critical-mass cap and the assertion layer

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
A diagnosis-first follow-up to ADR-0022 (owner request of 20 October 2026): why legitimate communication is missed by the interpretation path, and which restrictions are unnecessary. Done in this order: trace every missed legitimate row through every stage; verify the claim that the critical-mass cap causes most misses; change only what the evidence supports; guard each change with a legitimate paraphrase and its unsafe counterexamples; compare the unchanged baseline and each candidate on the same frozen splits by what the ENGINE accepts.

1. **Diagnosis** (`tools/bench/recall_diagnosis.py`, `engine_acceptance.py`, the CLI command `evidence-check`): per-example trace, every blocker, primary cause, safety cost of relaxing one check. Result (`docs/research/recall_diagnosis.md`): the cap is the primary blocker of 58 to 76 percent of misses, the only blocker of 34 to 50 percent, and it guards real safety (nine unsafe panel sentences leak without it).
2. **Engine evidence thresholds** (`tools/content/build_scoring.py` → `content/assessment/mapping.json`): the engine kept a single 0.8 placeholder ("replaced by the calibration export when one exists"); it now holds the classifier head's calibrated per-act thresholds floored to three decimals, so the engine never refuses what the gate credited.
3. **Critical-mass cap 0.03 → 0.05** (`content/assessment/act_head_hybrid.json`, `tools/bench/recall_cap_revision.json`, kept by `tools/bench/build_safe_heads.py`): the largest value of the grid with no leak on the safety panel and a margin of at least 1.5 times to the nearest unsafe sentence (rule frozen in `tools/bench/recall_frozen.json` before the held-out splits were scored). Rollback is one value.
4. **Assertion layer** (`server/classify/assertion.py`): one shared speaker-role list instead of four copies, delay markers widened to a class, first-person conditionals, denials with "say"; found by hand-written counterexamples, not by the benchmark (`reports/ai/recall-assertion-diff.json`: 64 of 84 counterexamples caught before, 84 of 84 now; no legitimate sentence newly flagged).
5. **Not changed, with the reason** (diagnosis section 5): cue false positives (genuinely ambiguous), fused-word repair (a benchmark artifact), context-constrained re-ranking (recovers nothing on dev), per-act caps, per-act thresholds. No other model or technique was run.

## Why
Recall was the weakest figure of the path (about 45 percent of legitimate synthetic sentences), and the benchmark counted what the service OFFERED, not what the engine ACCEPTED. Without a diagnosis a recall gain is either luck or a relaxed safety rule.

## How to implement
Already implemented; to repeat: `tools/.venv/Scripts/python.exe tools/bench/recall_diagnosis.py --split dev --write`, `recall_safety_panel.py --write`, `recall_eval.py --split dev --write` (selection), then `recall_eval.py --split test --final --write` and the same for `test2` ONCE per frozen candidate set (locked), `--confirm` for a labelled second look, `assertion_diff.py --write`, `recall_report.py`.

## Verification
`tools/ci/test_recall_fixes.py` (12 tests, registered): the cap and the thresholds are the recorded ones; 28 paraphrases and 13 recovered dev rows offered, none outside their window; 84 counterexamples never offered and each named by the assertion layer; the nine sentences only the cap guards stay guarded and a tripwire shows the fixture still exercises the cap. Engine: `InterpretationContextTests` (thresholds, boundary exact), `ScoringTests` T_EM3. `tools/bench/recall_report.py --check` (the page is what the result files give).

## Files changed
`tools/bench/recall_diagnosis.py`, `recall_safety_panel.py`, `recall_eval.py`, `engine_acceptance.py`, `assertion_diff.py`, `recall_report.py`, `recall_frozen.json`, `recall_cap_revision.json`, `build_safe_heads.py`, `tools/content/build_scoring.py`, `content/assessment/mapping.json`, `content/assessment/act_head_hybrid.json`, `content/manifest.json`, `server/classify/assertion.py`, `server/classify/ensemble.py` (earlier), `src/ProcSense.Cli/Commands.cs` (`evidence-check`), `tests/classifier_regression/recall_cases.json`, `tools/ci/test_recall_fixes.py`, `tools/ci/test_interpretation_bench.py`, `tests/ProcSense.Core.Tests/InterpretationContextTests.cs`, `ScoringTests.cs`, `tools/bench/interpretation_bench.py`, `interpretation_report.py`, `reports/ai/recall-*.json`, `reports/ai/interpretation-bench-*.json`, `docs/research/recall_diagnosis.md`, `docs/research/interpretation_benchmark.md`, `tools/ci/run_all.py`, `docs/CI.md`.

## Status (20 October 2026)
Built and tested (implementation), measured on synthetic data (preliminary). On the held-out action_bench splits, scored once: evidence accepted by the engine for the right act 63 to 76 of 140 (test) and 67 to 79 of 140 (test2), credited precision 76/76 and 79/79, no critical, non-assertive, out-of-window or safety-panel sentence accepted; the shipped configuration (with the assertion fixes) accepts exactly the same sentences on a second look. Without ANSWER_QUESTION and CLOSED_LOOP (which the benchmark gives a waiting question or read-back) the gain is 56 to 69 of 120 (test) and 58 to 69 of 120 (test2). **Independent validation: NOT DONE.** The independent set (EX-01) must be scored with this exact configuration before any claim; the `speech_interpretation` gate stays OFF. Quest and headset checks: NOT RUN (Sannan, DC-08 and DC-09).

## Dependencies
p1-ai-language-s10, s11, s14 (the path and its benchmark), the classifier gate of the removed step s07 (ADR-0029).

## Common issues
A benchmark that counts offers hides downstream refusals: always count what the engine accepts. A recall gain bought with a relaxed cap needs the panel run at the new value and the counterexample fixtures; a cap above the nearest unsafe sentence's critical mass leaks.

<!-- external-testing:start (generated by tools/plans/external_testing.py) -->
## External acceptance

This step has acceptance that only a person, device or account outside the engineering desk can give. The exact procedure, pass rule and evidence are in the single source of truth:

* [DC-09](../../../../testing/SANNAN_EXTERNAL_TESTING.md#dc-09): Spoken communication evaluated against the scenario state: graded communication credit, menu fallback and no credit for critical or non-performed speech (English only)
* [EX-01](../../../../testing/SANNAN_EXTERNAL_TESTING.md#ex-01): Independent evaluation set for the speech evaluator: authors, second labeller, adjudicator, then the single scored run (paused; a fresh held-out set may replace it)

<!-- external-testing:end -->
