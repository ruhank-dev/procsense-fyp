# ADR-0020 — How a trainee's communication act is recognised: keep the embedding gate, widen its assertion check, keep every model out of the active path

**Date:** 19 October 2026 (fifth session) · **Status:** Accepted for the engineering; the `classifier` feature stays OFF · **Related:** ADR-0005, ADR-0016, ADR-0018, ADR-0019; `docs/research/act_benchmark.md`, `docs/research/public_assertion_resources.md`

## Context
The classifier names the communication act a trainee's sentence performs (ask a history question, explain a test, reassure, ...) so the debrief can say what was heard. It is evidence only: deterministic action-based scoring is authoritative (ADR-0005) and a critical act is never credited from speech. The audit of the fourth-session state found the failures at four places:
* **The guard.** The critical-mass guard removed credit from 333 of 1,029 ordinary development sentences (`reports/ai/classifier-false-rejects.json`); no head can recover what a guard removes on purpose.
* **The assertion layer.** `server/classify/assertion.py` recognised some denials, hypotheticals and reports but not intentions put off, other speakers or other ways of reporting: 22 of 300 negated, intended, reported or other-speaker sentences were credited on the first frozen benchmark split.
* **Data.** Every earlier set was written by one author family; the v4, v5 and v6 sets each became development data once their failures were used, and two of them are now training rows. Nothing independent exists (EX-01).
* **Task design.** A "legitimate" label in the old sets depends on a reading of the sentence; the benchmark below labels by construction.

## Decision
1. **Benchmark** (`tests/action_bench_v1`, built by `tools/bench/build_act_dataset.py`, scored by `tools/bench/act_benchmark.py`): 28 seed families of ordinary acts, 10 of critical acts, put under assertion statuses by wrapper templates, with seeded speech-recognition damage; labels follow from the construction; splits by family and by wrapper template (checked in CI). Three splits: dev (may be used), test (scored once), test2 (written after test had been scored, with new families and new wrappers, scored once after the fix below). Synthetic, one author family, not independent.
2. **Five approaches on the same sentences** (frozen results, `docs/research/act_benchmark.md`):

| | Test split (488) | | | Test2 split (488) | | |
|---|---|---|---|---|---|---|
| | critical credited / 40 | non-assertive credited / 300 | legit recall | critical / 40 | non-assertive / 300 | legit recall |
| A rules only | 0 | 5 | 0.357 | 0 | 0 | 0.257 |
| B embedding head + learned status detector | 0 | 41 | 0.493 | 0 | 49 | 0.493 |
| C Qwen 4B + validation, no rules | **3** | 0 | 0.178 | **2** | 0 | 0.250 |
| C2 rules, then Qwen 4B | **2** | 6 | 0.436 | **2** | 0 | 0.343 |
| E existing gate | 0 | 22 | 0.472 | 0 | 0 | 0.493 |
| F E + status detector | 0 | 22 | 0.472 | 0 | 0 | 0.486 |

   Credited precision on test2: A 0.973, E 0.958, F 0.958, C2 0.906, C 0.814, B 0.570. Latency per sentence: A 0.1 ms, E 1 ms, B/F 1 ms, C and C2 about 3.0 s (one model call; 488 and 395 calls). Memory: the 4B model holds 3.1 GB of the 6 GB card while it runs; the Python process 206 MB; approach E needs only the 22 MB embedding model.
3. **Retained: E, the existing embedding gate, with its assertion check widened** (denials with more verbs, intentions put off, other speakers, other ways of reporting and imagining; `server/classify/assertion.py`). The widening was written from the failures of the first test split, which makes that split a development set; the fix was then scored once on test2, whose wrappers it had not seen: 0 of 40 critical and 0 of 300 non-assertive sentences credited, recall 0.493, precision 0.958. Regression fixtures and cases are kept (`tests/classifier_regression`); the old frozen sets stay as records.
4. **Rejected for the active path:** (C, C2) the model credited 2 or 3 of 40 critical sentences on each frozen split although every proposal passed the deterministic validation (examples from a later re-run, which is itself not identical to the scored run because the model is not deterministic: "Do you feel ready for us to proceed with the surgery?" read as a check of understanding, "The reason I'm calling is her high temperature." read as a fault report), and it costs 3 s a sentence on the desktop; it stays an evaluation tool and a bounded interpretation aid for the debrief only. (B, F) the learned status detector, trained on the dev wrappers, did not generalise to unseen wrappers (41 and 49 of 300 credited) and added nothing to E on test2 (F recall 0.486 against E 0.493); it was not shipped. (A) the rules are safe but credit about half as many legitimate sentences as E; they stay as the benchmark's deterministic reference and are not in the scoring path. (D) SetFit was not run: 488 development sentences from 28 families would teach the templates, it needs torch and sentence-transformers (several GB), and there is no valid held-out data on which it could beat E.
5. **Nothing here lets a classifier or a model grant critical-action credit.** The gate refuses every critical act, every critical-adjacent topic of the rules, every non-assertive sentence; a down embedder or model credits nothing; the scoring engine never reads this result.
6. **Public resources** (n2c2 2010 assertion, BioScope, NegEx/ConText) were researched, none used (`docs/research/public_assertion_resources.md`): access needs a signed agreement, the domain is clinical notes, not first-person speech. A later test on n2c2 data is EX-19.

## Consequences
The classifier stays OFF. What is established: on synthetic, constructed data from one author family the retained gate credits no critical or non-assertive sentence and about half of the legitimate ones at 96 percent precision; the set the widening was written from is a development set. What is not established: generalisation to real trainees' speech, to a second author, to a human-labelled set (EX-01, EX-18), and the safety gate's documented acceptance.

## Revisit if
An independent set exists; a second author's sentences show a different picture; a model with a calibrated critical-content detector appears; the critical-mass guard is replaced by a dedicated detector (P3-04).

## Addendum, 20 October 2026
The retained gate (decision 3) was changed on evidence in the recall follow-up of ADR-0022: the head's critical-mass cap went from 0.03 to 0.05 and the assertion check gained a shared speaker-role list, a wider class of delay markers and first-person conditionals; the previous behaviour, the evidence and the rollback are in `docs/research/recall_diagnosis.md` and `tools/bench/recall_frozen.json`. The 0/40 critical and 0/300 non-assertive figures above were re-measured on the revised gate (still 0).
