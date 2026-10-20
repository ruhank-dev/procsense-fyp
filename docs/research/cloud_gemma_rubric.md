# Ollama Cloud `gemma4:31b` as a context-conditioned semantic reader: dev, test and test2 (20 October 2026)

**Status: EXPERIMENT, development only, not wired to anything.** Synthetic, builder-written labels; no independent human review (EX-01); not clinically validated; no audio, Quest or headset test; not ready for live scoring. `speech_interpretation` stays OFF. Production guards, thresholds, prompts, benchmark labels and the frozen safety final set were not changed and not read. Every figure below is read from `reports/ai/cloud-gemma-rubric.json`, which `tools/bench/cloud_summary.py` computes from the raw replies (`artifacts/ai-bench/rubric/cloud-*.jsonl`, git-ignored; their sha256 prefixes are in the JSON).

## 1. Question and method
Does a larger open-weight model served by Ollama Cloud read trainee speech better than the existing classifier gate, if its output is only a *candidate interpretation* and deterministic code decides credit? The experiment reuses, unchanged, the contract of the earlier local Qwen 4B experiment (`docs/research/rubric_evaluator.md`): the 19 approved objectives of `content/assessment/taxonomy.json`, the annotation rubric's tie-breaks (prompt variant `clartb`), one call per utterance, per-objective verdict / actor / modality / evidence / reason, `rubric_eval.evaluate` (validation) and `rubric_eval.credit` (credit eligibility; a critical objective is never credited from speech), scored by `tools/bench/rubric_compare.py` against the recorded baseline gate on the same rows and labels. The adapter is `tools/bench/cloud_eval.py` (commit `cb85431`, 7 tests, no network).
* Model: `gemma4:31b` (cloud list digest `221b330d11a8`), requested as `gemma4:31b-cloud` through a signed-in local Ollama 0.40.5; the reply's own `model` field says `gemma4:31b` in every call. Free tier; no paid action. Temperature 0, seed 7, `think: low` (Gemma returned no reasoning trace), at most 1024 output tokens, 120 s timeout, one call per row, no retries.
* The service did not enforce the JSON schema for the models tried, so the prompt carries an explicit output-format instruction (`--hint`; fixed after the 98-row subset of dev, then not changed).
* Rows: `tests/action_bench_v1` `dev`, `test`, `test2`, 488 each (140 legitimate, 300 adversarial, 40 critical, 8 neither). No scenario state is available, so out-of-window credit cannot be measured here.
* **Strict is primary.** A reply counts as valid only if it is the JSON object the contract asks for. A reply wrapped in a markdown code fence is *malformed* under this contract. Section 4 shows a post-hoc view in which the fence is removed; it is secondary, labelled as such, and never replaces the strict figures.

## 2. Results (strict parsing, primary)
Credit views: *baseline* = the BENCHMARK baseline gate, i.e. the classifier gate as served with no scenario state, the system `rubric_compare.py` scores (NOT the shipped interpretation path, which adds the cap 0.05, the engine's thresholds and the validation of `server/interpret`, and was never combined with Gemma here); *model + guards* = the model's SUPPORTED findings, credited by `rubric_eval.credit` with the production critical-cue and non-assertive checks; *Gemma AND baseline* = credited only where the baseline gate credited the same act as well.

| split | system (baseline = benchmark gate, not the shipped path) | legitimate recall (of 140) | credited precision | critical false credit (of 40) | adversarial false credit (of 300) |
|---|---|---|---|---|---|
| dev | baseline gate | 83/140 | 83/83 | 0/40 | 0/300 |
| dev | Gemma, model rule only | 122/140 | 122/146 | 1/40 | 13/300 |
| dev | Gemma + production guards | 118/140 | 118/128 | 0/40 | 0/300 |
| dev | **Gemma AND baseline** | 73/140 | 73/73 | 0/40 | 0/300 |
| test | baseline gate | 66/140 | 66/70 | 0/40 | 0/300 |
| test | Gemma, model rule only | 123/140 | 123/151 | 4/40 | 13/300 |
| test | Gemma + production guards | 121/140 | 121/132 | 0/40 | 0/300 |
| test | **Gemma AND baseline** | 60/140 | 60/60 | 0/40 | 0/300 |
| test2 | baseline gate | 69/140 | 69/72 | 0/40 | 0/300 |
| test2 | Gemma, model rule only | 111/140 | 111/152 | 3/40 | 27/300 |
| test2 | Gemma + production guards | 111/140 | 111/126 | **3/40** | **1/300** |
| test2 | **Gemma AND baseline** | 57/140 | 57/57 | 0/40 | 0/300 |

Calls, format and speed (strict view; the first call of each split is excluded from latency, as in `rubric_compare.py`):

| split | calls | answered by | prompt tokens | output tokens | malformed (fenced JSON) | other defects | rows without a usable decision | latency median / p95 / max | calls over 5 s |
|---|---|---|---|---|---|---|---|---|---|
| dev | 488 | `gemma4:31b` 488 | 602,668 | 23,494 | 36 (7.4%) | 2 enum values | 45 | 678 ms / 1,188 ms / 12,774 ms | 1 |
| test | 488 | 488 | 602,653 | 23,144 | 38 (7.8%) | 4 enum values | 43 | 719 ms / 1,020 ms / 118,530 ms | 4 |
| test2 | 488 | 488 | 603,053 | 23,577 | 104 (21.3%) | 2 enum values | 105 | 686 ms / 1,925 ms / 77,710 ms | 10 |

No transport error, no timeout, no truncation; every call ended with the normal stop reason. Objective-level (strict): precision / recall / F1 0.830 / 0.894 / 0.861 (dev), 0.845 / 0.850 / 0.848 (test), 0.745 / 0.794 / 0.769 (test2). Usage cost is not exposed to the adapter; the owner read the account page ("99.1%" free before the first Gemma subset, 98.7% before the 488-row dev run, 97% before `test`, 97% confirmed before `test2`; the post-`test2` figure is not recorded here). The latency includes the network and queueing of a shared cloud service and is not comparable with the 14 ms of the local gate (median 14.4 ms, p95 19.0 ms, includes the embedding call).

## 3. What `test2` showed: the production guards alone do not consistently remove unsafe credit
On dev and `test` the production critical-cue and non-assertive checks removed every critical and adversarial false credit the model produced (critical 1/40 and 4/40 before the guards, 0/40 after; adversarial 13/300 before, 0/300 after). A reading of those two splits alone would say "model + guards is safe". **`test2` disproves that:** with the guards, 3 of 40 critical sentences and 1 of 300 adversarial sentences were still credited (strict view). Inspected afterwards for explanation only, with nothing tuned:
* an allergy question without an allergy cue word ("Is there a food you have to stay away from for your health?") credited as ASK_TOPIC, and the same sentence with a recognition error; the model named an ordinary act and the production critical-cue check did not refuse it (the sentence carries none of the cue words; the baseline gate refused it through its critical-mass cap);
* a handover-style request ("My request is that someone sees her within the hour.") credited as ESCALATE_SPEECH;
* one negation ("Sorry, I never got to say most people do well.") credited as REASSURE.
The same weakness appeared for the earlier local Qwen 4B reader: with the same guards it credited 2/40 and 6/40 critical sentences on `test` and `test2` (`reports/ai/rubric-eval-frozen-*.json`). So a larger model did not remove the dependence on lexical guards; the guards are exactly as good as their word lists.

## 4. Secondary, post-hoc: the same replies with the code fences removed
Not a valid strict output; shown only to separate "the model misread the sentence" from "the model wrapped correct JSON in a fence". Done by unwrapping a reply that is entirely a fenced block (files `cloud-gemma31b-fullfenced-*.jsonl`); nothing else is changed.

| split | rows without usable decision (strict → post-hoc) | Gemma + guards: recall, precision, critical, adversarial | Gemma AND baseline: recall, precision, critical, adversarial |
|---|---|---|---|
| dev | 45 → 9 | 121/140, 121/131, 0/40, 0/300 | 75/140, 75/75, 0/40, 0/300 |
| test | 43 → 5 | 122/140, 122/133, 0/40, 0/300 | 60/140, 60/60, 0/40, 0/300 |
| test2 | 105 → 1 | 122/140, 122/141, **5/40**, 1/300 | 61/140, 61/61, 0/40, 0/300 |

On `test2` the post-hoc view makes the guard failure larger (5/40 critical), because the unwrapped replies add two more critical sentences that the guards let through (not individually inspected). Cleaning fences is also unsafe to rely on as a design: it would turn a malformed reply into credit.

## 5. Recommendation: "Gemma AND baseline gate", as a research candidate only
Required agreement of the model (with the guards) and the benchmark baseline gate (not the shipped interpretation path) on the same act gave 0 critical and 0 adversarial false credits on all three splits, strict or post-hoc, and credited precision of 100 percent (73/73, 60/60, 57/57). That is the only form of the experiment with no observed unsafe credit. It is recommended **only as a research candidate for further, independent evaluation**, not as production-safe, not as clinically validated, and not for live scoring.
* **The recall trade-off is a loss, not a gain.** The AND form credits *fewer* legitimate sentences than the baseline alone: 73 against 83 (dev), 60 against 66 (test), 57 against 69 (test2). It removes the baseline's own wrong credits (4 of 70 and 3 of 72 on `test` and `test2`) and so raises precision, but it adds no recall. The recall gain of Gemma (111 to 123 of 140) exists only in the forms that include unsafe credit. A cloud model whose only safe use is to agree with a gate already in place has not been shown to be worth its latency, dependency and data exposure. The shipped interpretation path (95, 76 and 79 of 140 on the three splits, `docs/research/interpretation_benchmark.md`) is a stronger comparison than the bare baseline and was not combined with Gemma here.
* **Zero observed false credits is not a safety guarantee, and not evidence of clinical safety.** For the AND form, 0 of 40 critical and 0 of 300 adversarial rows (340 per split) were credited on each of the three synthetic splits. Read this only as a description of those rows. The usual "rule of three" (an upper limit of about 3 in 340, roughly 0.9 percent, at 95 percent confidence) assumes the rows are independent random draws from the population of interest. These are not: each split holds only 40 templated, builder-written families, all share one author and one wording style, and the guards and the baseline were developed on the same splits. That limit is therefore not a valid bound on any real rate, and no confidence interval or significance test is claimed in this report. The result says nothing about real learner speech, wordings outside these families (section 3 shows how a wording outside a word list passes), a recogniser that drops a negation, clinical consequences, or a provider update behind the same cloud name. The agreement with the baseline also inherits the baseline's own failure modes: both readers are blind to a critical statement that neither a cue nor the head recognises.
* An untested alternative that fits the evidence better: use the model only as a *veto* on a credit the baseline has already given. It was not run.
* Gemma was not tested in combination with the shipped interpretation path, so nothing here says what it would add to that path.
* A separate question the experiment cannot answer: whether sending trainee sentences to a cloud service is acceptable at all (see section 6).

## 6. Limitations
1. **Synthetic, builder-written labels.** `tests/action_bench_v1` is written by one author family and labelled by construction. No row has been reviewed by an independent person; independent validation (EX-01, DT-231, DT-234) is pending. Nothing here is evidence of generalisation.
2. **The baseline was tuned on these splits.** Its heads, thresholds and assertion rules were developed with dev and, for the assertion layer, with failures from `test` (`docs/research/recall_diagnosis.md`). The baseline numbers are therefore flattered, and the comparison is fair to Gemma only in the sense that the Gemma prompt was fixed on a 98-row subset of dev, not on `test` or `test2`.
3. **`test2` was not untouched.** It was used by the earlier Qwen rubric experiment (`rubric-eval-frozen-test2.json`) and by the recall work; its use for Gemma was a single run.
4. **The prompt was chosen on a dev subset** (98 rows of the 488 used here), so the 98-row result is not independent of the dev result. Differences of a few rows are not significant; no significance test was done and none is claimed.
5. **Malformed and fenced responses.** 7 to 21 percent of replies were fenced JSON, rejected under the strict contract; the share varied threefold across splits (36, 38, 104), which is itself a reliability risk. Enum values outside the allowed set also occurred (2, 4, 2).
6. **Cloud latency and availability.** Median under 0.8 s but occasional multi-second to 118 s calls; a dependency on a shared external service that can be unavailable, rate-limited or changed without notice. The local gate answers in about 14 ms.
7. **Data privacy and consent.** Utterances and approved definitions leave the machine. Only synthetic benchmark sentences were sent here. Sending real trainee speech to a third-party cloud would need a privacy and consent decision (see `server/PRIVACY.md`) that has not been made.
8. **Model identity.** The cloud name `gemma4:31b` may be updated by the provider; the digest in the list (`221b330d11a8`) is recorded, but results are tied to the date.
9. **Not performed:** audio or recogniser input, Quest or headset behaviour, scenario state (out-of-window credit is unmeasured), clinical review, a second model for comparison on `test` and `test2`. `gpt-oss:20b` was run only on a 98-row dev subset (19/28 recall with guards, 1/8 critical false credit, 98/98 schema failures without the format hint; `reports/ai/cloud-gemma-rubric.json`).

## 7. Reproduce
```
tools/.venv/Scripts/python.exe tools/bench/cloud_eval.py smoke --model gemma4:31b-cloud
tools/.venv/Scripts/python.exe tools/bench/cloud_eval.py run --model gemma4:31b-cloud --split dev   --tag gemma31b-full --max-calls 500 --stride 1 --hint
tools/.venv/Scripts/python.exe tools/bench/cloud_eval.py run --model gemma4:31b-cloud --split test  --tag gemma31b-full --max-calls 500 --stride 1 --hint
tools/.venv/Scripts/python.exe tools/bench/cloud_eval.py run --model gemma4:31b-cloud --split test2 --tag gemma31b-full --max-calls 500 --stride 1 --hint
tools/.venv/Scripts/python.exe tools/bench/rubric_compare.py --tag cloud-gemma31b-full --split test2 --stride 1
tools/.venv/Scripts/python.exe tools/bench/cloud_summary.py --write      # rebuilds reports/ai/cloud-gemma-rubric.json from the raw files
tools/.venv/Scripts/python.exe tools/ci/test_cloud_eval.py
tools/.venv/Scripts/python.exe tools/bench/safety_frozen.py --check
```
The cloud runs need a signed-in Ollama and use free allowance; each 488-row run was about 603k prompt and 23k output tokens. Do not rerun a split to obtain a better figure: a second run of `test` or `test2` makes it development data.

## 8. Reconciliation notes
* The p95 latencies in `cloud_summary.py` use the nearest-rank percentile; `rubric_compare.py` printed slightly different p95 values for the same calls (1.18, 0.96 and 1.87 s). The JSON figures are used here.
* The baseline in `rubric_compare.py` (83, 66 and 69 of 140) is the classifier gate with no scenario state; the interpretation benchmark's 95, 76 and 79 of 140 add the shipped cap and engine thresholds. They are different systems.
* Before the 488-row dev run the account reading (98.7%) covered about 305 calls; the readings are the owner's, not the adapter's.
