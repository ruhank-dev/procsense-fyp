# p1-live-scenario-generation-s04 — AI composition: narrative, profile wording, persona (LG-06)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
LLM fills narrative slots through the AI gateway with a JSON schema; grounding validator; template fallback.

## Why
Language is the product here; values are not.

## How to implement
1. Prompt `prompts/compose/v1.txt`; schema with enums.
2. One retry with defect avoid-hint; then template.
3. Persona tags vocabulary (DT-085).

## Status (17 October 2026)

- **Built:** `server/llm/` (Ollama client with every failure as a coded reply, the hashed prompt registry `prompts/registry.json`, the gateway function that validates against a schema and a validator, retries once with the reasons and falls back) and `server/generation/narrative.py`: a speaking style (two of the six persona tags of `content/ai/persona.json`, DT-085, chosen by the seed) rewrites up to six of the patient's lines (the reason for admission and history answers of the base path); a line is accepted only if the numbers, number words, time words, yes and no, medicines, tests, conditions, allergens and the words of the original are kept and nothing is prescribed; otherwise one retry with the reasons and then the authored line stays. The structured case is never given to the model.
- **Measured** (`tools/ai/eval_narrative.py`, `reports/ai/narrative-*.json`): the 4B model, 132 lines of 25 approved cases: schema valid first try 100.0 percent (target 95), accepted by the checks 80.3 percent (55.3 percent first try), 26 fell back to the authored line, p50 1617 ms. The 9B: accepted 78.7 percent, p50 4358 ms; no better, so the 4B is used.
- **What the first real run found:** the checks I wrote first let through an invented duration ("only about ten minutes since I arrived"), and a no turned into a vague line; number words, time words, yes and no and kept-word checks were added, and a persona tag that invited invented remarks ("chatty") was removed.
- **Limit:** "zero grounding escapes" is not proved by lexical checks: every accepted pair is in the result file, 38 of them read by me found none unfaithful, and a reader's judgement of all of them is open (DT-203).

## Verification
First-try schema validity at least 95 percent; zero grounding escapes.

## Files changed
`server/generation/compose.py`, `prompts/compose/`

## Dependencies
p1-ai-language-s02, p1-backend-s06, p1-live-scenario-generation-s03

## Common issues
Model drifts into values: values are hidden or slot tokens in the prompt.

## Sources
Spec LG-06; A3, C29.
