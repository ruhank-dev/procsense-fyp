# p1-live-scenario-generation-s13 — AI scenario plan composer: LLM with retrieval, structured generation and solver completion (LG-04)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
The live and batch composer: an LLM receives objectives, difficulty, history summary, a retrieved knowledge-base subgraph and exemplar cases, and returns a ScenarioPlan (problems, comorbidities, investigations with expected findings and band targets, decision points with traps, events, branches, outcomes, rationale per choice) as schema-constrained JSON over approved ids; the deterministic solver completes and checks it; Stage B and cross-modal rules validate it.

## Why
Random recombination is not AI generation; this is the component that composes coherent cases from evidence and explains them.

## How to implement
1. `server/generation/compose_plan.py`; schema `server/schemas/scenario_plan.json` with enums of KB concept and block ids.
2. Prompt `prompts/compose_plan/v1.txt` with rationale fields; every relation the plan relies on must exist in the KB.
3. Solver completion and constraint check; defects returned as avoid-hints (max 3 rounds).
4. Blind comparison with the solver-only baseline (40 cases, two raters).
5. Latency measured on both GPUs; batch mode fills the pool.

## Status (17 October 2026)

- **Built:** `server/generation/compose_plan.py` (`Planner`): two small questions to the 9B model, each with a JSON Schema whose enums list only what is allowed (which of the best eight ranked approved cases keeps the story; which blocks fill each slot, a name being a first-name and a last-name slot); the proposal is checked by the composer's own rules (`Solver.violations`, slot counts, status), sent back with the reasons up to 3 rounds, then dropped for the solver. `solve.build_card_from_blocks` builds a card from chosen blocks. Prompts `compose_plan.spine` and `compose_plan.blocks`. Recorded in the package (model digest, prompt hashes, reasons). The model sees case labels, tiers and problem labels, never a result or a hidden fact.
- **Measured** (`tools/ai/eval_planner.py`, 9B, 20 requests of a simulated trainee with ratings): planner served 19 against 20 for the solver alone, all passed the whole gate; coverage of the requested dimensions 0.685 against 0.746; difficulty match 0.955 against 0.957; 19 distinct block sets in 20 (T-SA3 asks 12); 8 stories against 11; 30.0 s a request against 0.1 s. The model's proposals were accepted by the composer in about 76 percent of rounds (mean 1.32 rounds).
- **Verdict:** the planner is valid and diverse but did not beat the solver on coverage or difficulty and costs 300 times the time, so it is built, tested and **off by default**. The blind comparison by two raters on 40 cases is open (DT-203).
- **Found:** the repair time budget counted the model's own calls and sent slow requests straight to the fallback; the budget now starts when repair starts.

## Verification
Schema validity at least 95 percent; coherence and objective coverage higher than solver-only (paired, p under 0.05) at equal Stage B pass rate.

## Files changed
`server/generation/compose_plan.py`, `server/schemas/scenario_plan.json`, `prompts/compose_plan/`

## Dependencies
p1-dataset-analysis-s06, p1-live-scenario-generation-s03, p1-ai-infrastructure-s02, p1-personalisation-s03

## Common issues
Model invents relations: enum schema plus KB relation check reject them.

## Sources
Features A1, A2, A4, A5, A7, A8.
