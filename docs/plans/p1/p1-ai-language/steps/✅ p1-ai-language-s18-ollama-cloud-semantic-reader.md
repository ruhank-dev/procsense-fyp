# p1-ai-language-s18 — Ollama Cloud semantic reader: strict structured output, provider interface, integrated pipeline, tests and release-candidate evidence

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
Owner request of 20 October 2026 (prompt 178): take the speech interpretation from experiment to a tested release candidate on the chosen platform, **Ollama Cloud**, without weakening a safety control. The gate-based path of ADR-0022 and ADR-0023 is the fallback and the safety floor; a large model is added as a READER whose output is only a candidate (ADR-0024).

1. **Catalogue and capability verification** (public cloud list and official pages, 20 Oct 2026): 23 cloud models; no Ministral and no Qwen3.5 397B; no cloud model advertises structured outputs; the official page says cloud does not support them, and a controlled test reproduced it (6 of 6 replies non-conforming with `format` and with `response_format`).
2. **Structured output**: Pydantic `Reading` (`semantic-reading-1`), prompt-grounded, strictly validated, never repaired (fences, extra keys, bad enums, unknown or duplicate objectives, fabricated evidence, truncation, text around the object are rejected), bounded retries, safe fallback (`server/interpret/llm/`).
3. **Provider interface**: Ollama Cloud (direct API with a Bearer key or a signed-in local relay), mock, replay; secrets as `SecretStr`; no credential in a body or a log; settings in `server/config.py` and `server/.env.example`; privacy note in `server/PRIVACY.md`.
4. **Pipeline** (`server/interpret/semantic.py`, built only when `interpret_llm_enabled`): shipped Interpreter first; the model is not called for critical topics or any non-performed sentence; a candidate must pass the shipped deterministic validation and the head's critical-mass cap; confidence is the head's probability; the model can veto what the gate offered; any reader failure returns the shipped decision.
5. **Tests**: 65 server tests without the cloud (strict validator, provider, reader, prompt, interpreter policy, scenario-state interactions, injection, fallbacks, concurrency, wiring, budget guard); 18 mutants of the new code killed.
6. **Evidence** (`tools/bench/semantic_eval.py`, `docs/research/semantic_reader_pipeline.md`): the integrated pipeline replayed on the recorded Gemma replies of dev, test and test2: 0 critical, 0 adversarial, 0 out-of-window offers; recall 88, 75, 75 of 140 against 95, 76, 79 for the shipped path; strict validity of recorded replies 92.2, 91.4, 78.3 percent. Acceptance table in ADR-0024.
7. **Model selection**: shortlist `gemma4:31b` (measured), `deepseek-v4.1-flash`, `mistral-large-4`. The owner approved the bounded runs on 20 Oct; the comparison is **BLOCKED** (`deepseek-v4.1-flash`: HTTP 402, needs the paid Pro plan; `mistral-large-4`: HTTP 403, not rolled out to the account); no paid plan was enabled. The live `gemma4:31b` run on 488 dev rows went to the model for 141 rows: 137/137 replies valid, 4 timeouts (a defect, fixed), pipeline recall 92/140 at 92/93, 0 critical, 0 adversarial, 0 out-of-window. A follow-up on the free plan (`gpt-oss:120b`: 79/140, 99.3% valid; `nemotron-3-super`: 88/140, 79.4% valid, 3.5 s median) did not beat Gemma (92/140). `gemma4:31b` is the provisional target; no model is ready; the model is a setting.

## Why
The experiment showed a large recall gain only in forms that included unsafe credit, and a reply format that the platform does not constrain. A release candidate has to keep the deterministic authority, make format failures harmless and measurable, and say honestly what it does and does not gain.

## How to implement
Already implemented; to repeat the evidence: `server/.venv/Scripts/python.exe -m pytest server/tests/test_semantic_*.py`, `tools/bench/semantic_eval.py --split dev|test|test2 --mode head --write`, `tools/ci/mutation_safety.py --engine --write`. To run the live measurements after approval: `tools/bench/semantic_eval.py --split dev --live gemma4:31b-cloud --stride 1 --max-calls 500 --write`.

## Verification
`server/tests/test_semantic_reader.py`, `test_semantic_interpreter.py`, `test_semantic_eval_tool.py` (65), the unchanged `test_interpret_api.py` (9, PostgreSQL), `tools/ci/test_mutation_safety.py` (SemanticGuards), `tools/bench/safety_frozen.py --check` (the frozen candidate files, sets and final results are untouched).

## Files changed
`server/interpret/llm/{__init__,schema,prompt,strict,provider,reader}.py`, `prompt_wording.json`, `server/interpret/semantic.py`, `server/config.py`, `server/routers/interpret.py`, `server/.env.example`, `server/PRIVACY.md`, `server/tests/test_semantic_*.py`, `tools/bench/semantic_eval.py`, `tools/ci/mutation_safety.py`, `tools/ci/test_mutation_safety.py`, `reports/ai/semantic-replay-*.json`, `docs/decisions/ADR-0024-ollama-cloud-semantic-reader.md`, `docs/research/semantic_reader_pipeline.md`, plans and registers.

## Status (20 October 2026)
Built and tested (engineering) behind two OFF switches. **Not met:** recall 85 percent (65.7 percent live on dev, 63, 54, 54 percent by replay; ceiling set by the critical-mass cap), independent labels; schema compliance 99.5 percent met on replies received but not on requests (4 timeouts of 141). **Not done:** the model comparison (blocked by plan and rollout; any other free-plan model needs approval), privacy and consent decision, independent validation (EX-01), real audio, Quest (DC-08, DC-09; Sannan), clinical review. `speech_interpretation` and `interpret_llm_enabled` stay OFF.

## Dependencies
p1-ai-language-s10 to s17 (the path, the hardening, the frozen sets), the cloud experiment (`docs/research/cloud_gemma_rubric.md`).

## Common issues
The cloud route does not enforce a schema: never rely on `format`; never strip a fence. A candidate below the engine threshold is abstained with a code instead of being refused later. The recorded replies used the bare prompt, so replay results do not measure the scenario-context prompt.

<!-- external-testing:start (generated by tools/plans/external_testing.py) -->
## External acceptance

This step has acceptance that only a person, device or account outside the engineering desk can give. The exact procedure, pass rule and evidence are in the single source of truth:

* [DC-09](../../../../testing/SANNAN_EXTERNAL_TESTING.md#dc-09): Spoken communication interpreted against the scenario state: evidence, clarification, menu fallback and no credit for critical or non-performed speech (English only)
* [EX-01](../../../../testing/SANNAN_EXTERNAL_TESTING.md#ex-01): Independent communication-act set: authors, second labeller, adjudicator, then the independent run

<!-- external-testing:end -->
