# p1-ai-language-s08 — AI debrief (EXP-AI-4)

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
AI rewrites the template debrief from DebriefFacts in the walkthrough's debrief structure (what you missed, what you recognised, highest-priority problem and why, related vs independent findings, escalation triggers, consequence if ignored, what to remember); every sentence carries fact IDs; validator requires all critical items covered and verdicts/numbers unchanged; template shown immediately, AI version replaces it when validated.

## Why
A rewritten debrief can be friendlier and more personal than templates — but a debrief that invents or changes a mistake would teach the wrong lesson, so the validator is stricter here than anywhere else.

## How to implement
1. Prompt v1 per guide; num_predict 600; async ≤ 8 s.
2. `tools/ai_eval/debrief_mutation.py` over 50 sessions.
3. Template debrief shows immediately; AI version replaces it only after validation passes.

## Status (17 October 2026)

- **Built:** `server/debrief/rewrite.py` and `POST /debrief/organisations/{id}/rewrite`: every scored item of the report becomes a fact with a verdict and numbers; the TEMPLATE debrief is made first and is complete; the model rewrites the facts into sections (titles from a closed list, fact ids from the facts); the answer replaces the template only if the strict validator passes: every fact covered, no number changed, no praise turned into a mistake or the reverse (a section may not mix verdicts), no new event, medicine, test, condition or equipment, no dose, no prescribing phrase. Prompt `debrief.rewrite`.
- **Mutation test** (`tools/ci/test_llm.py`): 19 corruptions are refused; a known gap: an event that is not an engine event passes (DT-211).
- **Measured** (`tools/ai/eval_debrief.py`, qwen3.5:4b-q4_K_M, 16 golden sessions): the rewrite replaced the template in 11 (68.8 percent), the template stayed in 5 (reasons {'DEBRIEF_VERDICT_CHANGED': 5, 'DEBRIEF_MISSING_FACT': 4, 'DEBRIEF_MIXED_VERDICT': 1}); latency p50 7357 ms, within 8 s 62.5 percent; corrupted rewrites refused 43 of 43.

## Verification
T-DB1–T-DB5 pass; offline shows the template only.

## Files changed
`server/routers/debrief.py`, `prompts/debrief/v1.txt`, `server/schemas/debrief.py`, `tools/ai_eval/debrief_mutation.py`, Unity debrief AI tab.

## Dependencies
p1-ai-language-s01; p0-scoring-debrief-s04.

## Common issues
AI drops minor items → validator requires every item ID covered; generic feedback → include evidence spans in DebriefFacts.

## Sources
Implementation Guide B-R9 Steps 1–5, T-DB1–T-DB5; walkthrough debrief structure (Part D §20).
