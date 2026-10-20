# Semantic reader pipeline on Ollama Cloud: catalogue, structured output, and the integrated pipeline replayed on dev, test and test2 (20 October 2026)

**Status: release candidate under development, behind two OFF switches (`interpret_llm_enabled`, `speech_interpretation`).** Decision record: `docs/decisions/ADR-0024-ollama-cloud-semantic-reader.md`. Everything measured here is **synthetic and builder-written** (`tests/action_bench_v1`), the baseline was tuned on parts of these splits, `test2` was used by the earlier Qwen experiment, there is no independent human review (EX-01), and nothing was tested with audio, on a Quest or clinically. The historical Gemma results (`docs/research/cloud_gemma_rubric.md`) are unchanged evidence from a different configuration (no pipeline, fence-stripped views, word-list guards only).

## 1. Catalogue (public list `https://ollama.com/api/tags`, read 20 Oct 2026, 23 models)
All entries advertise `completion`, `thinking` and `tools` (some `vision`); none advertises structured outputs. Cloud names are the direct-API names; through a signed-in local Ollama the `-cloud` form is used.

| model | listed size | updated | use for this task |
|---|---|---|---|
| `gemma4:31b` | 62.5 GB | 2026-04-02 | **measured** (dev, test, test2); working target |
| `deepseek-v4.1-flash` (`:1017`) | 486.7 GB | 2026-09-10 (10-17) | shortlist: newest flash-class model |
| `mistral-large-4` | not listed | 2026-10-06 | shortlist: Mistral family (no Ministral model is listed) |
| `glm-5.3-flash` | 328.3 GB | 2026-08-26 | first substitute for the DeepSeek flash class |
| `gpt-oss:20b` / `:120b` | 13.8 / 65.3 GB | 2025-08-05 | `:20b` measured on 98 dev rows: weak; `:120b` not measured |
| `deepseek-v4-pro:0813`, `glm-5.3`, `kimi-k3`, `kimi-k2.6/2.7`, `minimax-*`, `nemotron-3-super/ultra` | 230 GB to 1.56 TB or not listed | 2026 | excluded: interactive latency and usage cost unknown or high |
| Qwen3.5 397B | **not in the list** | | cannot be evaluated on this platform now |
Licences and per-model price were not exposed by the list; the owner's account shows only a percentage of the free allowance. Usage per run is therefore estimated from the measured Gemma cost (about 1.7 percent of the allowance for 488 rows, 603k prompt tokens), not known for other models; a thinking model may use more output tokens.

## 2. Structured output on the cloud route: not supported, verified
* Official page: "Ollama's cloud currently does not support structured outputs." (https://docs.ollama.com/capabilities/structured-outputs), and the cloud page lists no schema feature.
* Controlled test, 3 sentences on `gemma4:31b`, the full rubric prompt, temperature 0: native `format` (a JSON schema) on `/api/chat` and OpenAI-style `response_format` json_schema on `/v1/chat/completions`: **6 of 6 replies did not conform** (code fences; one used an invented key set).
* Consequence: native constrained generation is unavailable on the intended route; the schema is sent for grounding and every reply is validated strictly (ADR-0024 decision 2 and 3).

## 3. What was built (all under `server/`, tested without the cloud)
`interpret/llm/schema.py` (Pydantic, `semantic-reading-1`), `prompt.py` (the evaluated `clartb` prompt, equality-tested; untrusted-data declaration; JSON-escaped utterance), `strict.py` (never repairs), `provider.py` (Ollama Cloud, mock, replay), `reader.py` (bounded retries), `semantic.py` (policy), `config.py` / `.env.example` (settings, `SecretStr` key), `routers/interpret.py` (built only when enabled, audit by reason code and model). Tests: `server/tests/test_semantic_reader.py` (27: every defect of the task list and of the experiments), `test_semantic_interpreter.py` (32: agreement, veto, addition, critical, `test2` case, non-performed, window, waiting state, duplicate credit, injection, fallbacks, state change, concurrency, wiring), `test_semantic_eval_tool.py` (3). `tools/ci/mutation_safety.py`: 17 mutants of the new code, all killed.

## 3a. Defects found while building, each with a regression test
* The prompt for the experiments was bare; the production prompt adds context and declares speech as data: an injected "ignore the rubric" inside an utterance cannot end the data block (JSON-escaped), and a sentence the shipped checks refuse is never sent.
* A model reading that the gate had offered but the model did not support would otherwise be credited: the veto is tested (and mutated).
* A model candidate for ANSWER_QUESTION or CLOSED_LOOP without a waiting question or read-back is refused (the experiments' guards did not cover this because the benchmark gives that context).

## 4. The integrated pipeline replayed on the recorded Gemma replies (strict validation, no cloud call)
`tools/bench/semantic_eval.py` replays the recorded raw replies (`artifacts/ai-bench/rubric/cloud-gemma31b-full-*.jsonl`) through the real pipeline and the engine's evidence store, with the benchmark's per-row state (`tools/bench/interpretation_bench.py`). The fence-stripped files are never used: a fenced reply is INVALID and the pipeline falls back to the shipped decision. A retry cannot change a replayed reply, so format failures are a worst case. Denominators: 140 legitimate, 300 adversarial, 40 critical per split.

| split | system | offers | credited precision | legitimate recall | critical false credit | adversarial (non-assertive) false credit | out-of-window offers |
|---|---|---|---|---|---|---|---|
| dev | shipped path (cap 0.05, engine thresholds) | 96 | 95/96 | 95/140 | 0/40 | 0/300 | 0 |
| dev | **pipeline, default (head confidence)** | 89 | 88/89 | 88/140 | 0/40 | 0/300 | 0 |
| dev | pipeline, `engine_floor` (not approved) | 94 | 91/94 | 91/140 | 0/40 | 0/300 | 0 |
| test | shipped path | 76 | 76/76 | 76/140 | 0/40 | 0/300 | 0 |
| test | **pipeline, default** | 75 | 75/75 | 75/140 | 0/40 | 0/300 | 0 |
| test | pipeline, `engine_floor` | 82 | 82/82 | 82/140 | 0/40 | 0/300 | 0 |
| test2 | shipped path | 79 | 79/79 | 79/140 | 0/40 | 0/300 | 0 |
| test2 | **pipeline, default** | 75 | 75/75 | 75/140 | 0/40 | 0/300 | 0 |
| test2 | pipeline, `engine_floor` | 79 | 78/79 | 78/140 | 0/40 | 0/300 | 0 |

Strict validity of the recorded replies (first attempt): dev 450/488 (92.2%; 36 fenced, 2 schema), test 446/488 (91.4%; 38 fenced, 4 schema), test2 382/488 (78.3%; 104 fenced, 2 schema). Tokens of the recorded runs: about 603k prompt and 23k output per split. Reports: `reports/ai/semantic-replay-cloud-gemma31b-full-{head,engine_floor}-{dev,test,test2}.json`.

**Reading.**
* Safety: with the cap and the deterministic checks applied to the model's candidate, no critical, adversarial or out-of-window sentence was offered, including the `test2` allergy question that the word-list guards alone had let through. This is zero observed on synthetic rows, not a guarantee.
* Recall: the pipeline is **not better than the shipped path** (-7, -1, -4 sentences) and far from the 85 percent target. Transitions against the shipped offers on the three splits: 2, 6 and 4 sentences added, 9, 7 and 8 lost. Losses come from the model reading a sentence as a different objective than the benchmark label, from the model's veto, and from candidates that fail the window or the engine threshold.
* Why the ceiling is low: of the legitimate sentences not offered under `engine_floor`, 33 of 49 (dev) and 40 of 62 (test2) are blocked by the head's critical-mass cap and 6 and 13 by the critical-cue menu, i.e. by the protections that must not be relaxed for recall alone. A better reader cannot raise recall past them; an independent critical-content signal could (DT-238).

## 5. Acceptance status
See the table in ADR-0024: recall NOT MET; precision, critical and adversarial met on synthetic rows only; schema compliance NOT MET on recorded replies (live retry unmeasured); latency provisional; availability tested by unit tests only.

## 6. Not done, and what needs approval
1. **Bounded model comparison** (DT-237): `gemma4:31b`, `deepseek-v4.1-flash`, `mistral-large-4` on the same 98 dev rows with the same prompt and strict validation: about 98 calls per new model, about 125k prompt tokens each plus unknown thinking output; estimated 0.4 to 1 percent of the free allowance per model (Gemma: 0.35 percent per 98 rows). **Not run: needs the owner's approval of the usage.**
2. **Live production-reader run on dev** (DT-237): `server/.venv/Scripts/python.exe tools/bench/semantic_eval.py --split dev --live gemma4:31b-cloud --stride 1 --max-calls 500 --write`: measures strict compliance with the new format instruction and one retry, latency and tokens through the real reader; estimated about 1.7 percent of the allowance for 488 rows. **Not run.**
3. Everything external: independent labels (EX-01), privacy and consent decision for third-party processing (`server/PRIVACY.md`), real-audio and Quest tests, clinical review. `speech_interpretation` stays OFF.

## 7. Reproduce
```
server/.venv/Scripts/python.exe -m pytest server/tests/test_semantic_reader.py server/tests/test_semantic_interpreter.py server/tests/test_semantic_eval_tool.py -q
server/.venv/Scripts/python.exe tools/bench/semantic_eval.py --split dev  --replay cloud-gemma31b-full --mode head --write      # likewise test, test2, and --mode engine_floor
tools/.venv/Scripts/python.exe tools/ci/mutation_safety.py --engine --write
tools/.venv/Scripts/python.exe tools/bench/safety_frozen.py --check
```
The replay needs the raw files under `artifacts/ai-bench/rubric/` (git-ignored; hashes in `reports/ai/cloud-gemma-rubric.json`).
