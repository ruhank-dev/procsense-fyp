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
