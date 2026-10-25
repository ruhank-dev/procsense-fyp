# ADR-0026 — Each AI task has its own configured model; Ollama Cloud (free plan) serves the tasks where a benchmark showed a clear gain, behind two switches and a local fallback

**Date:** 25 October 2026 · **Status:** Accepted for the engineering (owner request of 25 Oct 2026). Both switches ship OFF; trainee-data routes wait for the privacy decision (DT-248). · **Related:** ADR-0010, ADR-0011, ADR-0017, ADR-0024, ADR-0025, DT-248, DT-250

## Context
* Every language-model task except the speech evaluator (ADR-0025, `gemma4:31b` on Ollama Cloud) ran on the local `qwen3.5:4b-q4_K_M` (ADR-0010, ADR-0017). The owner asked to choose each task's model by measurement, using the free Ollama Cloud allowance already in use, without changing ASR, TTS or the speech evaluator.
* Catalogue and access (25 Oct 2026, through the signed-in local Ollama): the public list `https://ollama.com/api/tags` holds 23 models. Reachable on this account's Free plan: `gemma4:31b`, `gpt-oss:20b`, `gpt-oss:120b`, `nemotron-3-nano:30b`. HTTP 402 "not in the Free plan": `deepseek-v4.1-flash:1017`, `deepseek-v4-pro:0813`, `mistral-large-3:675b`. Not found for this account: `deepseek-v4.1-flash`, `glm-5.3`, `glm-5.3-flash`, `glm-5.2`, `kimi-*`, `minimax-*`, `mistral-large-4`, `nemotron-3-super`, `nemotron-3-ultra`, `muse-spark-open`, `beam`. `clef:27b` is a decision model without a chat route. The Free plan has 1 concurrent request and "a starter amount of usage credits" that reset monthly; the amount is not stated on the pricing page, so no capacity figure is claimed. Ollama states that cloud prompts are not used to train models.
* The cloud route does not enforce a JSON schema (ADR-0024): `gemma4:31b` wraps JSON in a code fence unless told not to (82 of 82 dialogue replies were rejected by the strict parser until the format instruction was added); `gpt-oss` always thinks, so a small token budget returns nothing.
* All benchmarks below use the repository's own harnesses (`tools/ai/eval_*.py`, production code paths) on authored synthetic cases and AI-written synthetic test sets. No real trainee audio, speech or patient data was sent.

## Decision
1. **Routing** (`server/llm/routing.py`, `content/ai/model_routes.json`): the gateway resolves the model per task from the route file. Each route has the model, a data class (`synthetic`: authored content only; `trainee`: anything a trainee said or did), the evidence, and for a cloud model the local fallback.
2. **Three gates for the cloud:** `CLOUD_ROUTING_ENABLED` (off), the route naming a cloud model, and for `trainee` routes also `CLOUD_TRAINEE_DATA_APPROVED` (off; waits for DT-248). With any gate closed the task uses `qwen3.5:4b-q4_K_M` exactly as before.
3. **Local fallback:** a cloud outage, an unknown model, a usage limit (HTTP 402 or 429), a timeout, an invalid or fenced reply or a failed validator is retried once on the local model; a second failure gives the caller its template, as before. The audit row records the route, the data class and `fell_back_from`.
4. **Cloud replies are validated as strictly as local ones** (JSON schema, grounding and style validators); a code fence is rejected, not repaired. The format instruction (plain JSON, schema included) and a thinking-token budget are added only for cloud models.
5. **Selection rule** (`tools/ai/cloud_routing_report.py`, fixed before the choice): real-time dialogue moves only if quality is at least equal, leaks are no higher and the 95th percentile is within the 4 s budget; offline tasks move only if quality is better by at least 5 points with no higher false-alarm rate; speed alone never moves an offline task; no benchmark means local. The decisions are stored in `reports/ai/cloud-routing-benchmark.json` and re-derived in CI.
6. **Selected models** (rows: task, local figure, cloud figure, `gemma4:31b-cloud` unless stated; synthetic data):
   * Patient dialogue (82 earned, 82 withheld questions on 12 approved cases): fact used 74 → 82 of 82; withheld questions answered "cannot recall" 76 → 81 of 82; lexical leaks 1 → 0; median 1,882 → 684 ms, p95 2,289 → 1,716 ms. `gpt-oss:120b` 80/82 cannot recall, p50 979 ms; `gpt-oss:20b` 72/82, p50 2,034 ms; `nemotron-3-nano:30b` 34/82 with 7 leaks (rejected).
   * Staff intent (16 sentences × 3 repeats, both roles in one call): accepted 70.8% → 93.8% (nurse 58.3% → 87.5%, anaesthesia 83.3% → 100%), p95 1,517 → 717 ms; `gpt-oss:120b` 89.6%, p95 3,616 ms. Three voices answering one turn: p95 3.55 s → 2.09 s (`gpt-oss:120b` 3.72 s).
   * Scenario narrative (106 lines): accepted 71.7% → 92.5%, fallbacks to the authored line 30 → 8, median 2,643 → 672 ms (`gpt-oss:120b` 88.7%).
   * Text critic (214 seeded defects): recall 68.8% → 89.3%, false alarms 3.1% → 0% (`gpt-oss:120b` 85.6%, 3.1%). Scenario judge: flags 36.7% → 90.0% of the seeded defects, 0 clean cases flagged. Trap writer: accepted 23.5% → 71.2% with fairness not lower.
7. **Kept local** (`qwen3.5:4b-q4_K_M`): the composition planner (all arms pass the gate with coverage 1.0, but the cloud plans were less varied, 4 distinct block sets of 10 against 10), plan events and interactions (equal), debrief rewrite (accepted 16/16 locally against 15/16 on both cloud models, which were faster), the performance interpreter (confirmed 85.4% against 85.5%), and the tasks with no cloud benchmark (analytics explanation, spoken-order fallback). Speech recognition (faster-whisper `small.en`), speech output (Piper) and the speech evaluator (`gemma4:31b`, prompt `gemma-eval-5`, ADR-0025) are untouched.
8. **Nothing model-written awards credit.** Staff speak fixed approved lines of the intent chosen; the patient's reply must pass the grounding validator; the engine, not these models, scores. The speech evaluator's contract is unchanged.

## Alternatives
* **Cloud for everything:** rejected; the planner, debrief and interpreter showed no gain, and the Free plan has one concurrent request.
* **A bigger paid model per task:** not possible on the Free plan and out of scope (no billing).
* **MedGemma 4B for dialogue:** DT-222 measured parity with Qwen at best; it adds terms to accept and no measured gain; not routed.
* **Repair a fenced reply instead of asking for plain JSON:** rejected (ADR-0024); a repair path turns a malformed reply into content.
* **Enable the cloud routes by default:** rejected; usage and privacy are the owner's decisions.

## Consequences
* With the switches on, offline generation (narrative, critic, judge, trap writer) can use the cloud now (synthetic data only). Live dialogue and staff stay on Qwen until DT-248 is decided; the route and its tests are ready.
* Dialogue and staff quality figures are lexical and synthetic: the withheld replies still need a person to read them (DT-190, DT-203); the staff set is AI-written (DT-250).
* Usage is unmetered here: a monthly allowance that runs out gives HTTP 402 or 429, handled as an outage.

## Revisit if
* DT-248 is decided (enable the trainee routes), the plan or model catalogue changes, `qwen3.5` is replaced, or human blind ratings (DT-250) disagree with these figures.
