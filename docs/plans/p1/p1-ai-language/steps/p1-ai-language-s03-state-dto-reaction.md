# p1-ai-language-s03 — Read-only context DTOs and the reaction layer

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
`ContextDTO` (immutable, per-character projection: stage, completed/missing goals, disclosed facts, disclosable-now facts, told-to-patient, behaviour view, signs, active events, last turns, results, difficulty) built in Core; a separate `ProcSense.AIClient` assembly that can only see DTO types (no write path); reaction triggers (action/event/state threshold, debounce, at most one spontaneous patient utterance per 8 s sim); `/behaviour` realisation; intent gate (suggested intents mapped via `intent_map.json`, unknown dropped + audited); DTO trimmed to ≤ 900 tokens.

## Why
AI must never mutate simulation truth (T-AI-03 / T-RL1).

## How to implement
1. `src/ProcSense.Core/AIContext/ContextBuilder.cs`; unit tests per projection.
2. asmdef reference restriction for the AIClient assembly.
3. `ReactionQueue` with priority (event > question > spontaneous).
4. DTO token budget measured with the model tokenizer; oldest turns dropped first.

## Verification
T-RL1 (no write API, compile-time), T-RL2…T-RL6 green.

## Files changed
`src/ProcSense.Core/AIContext/{ContextDTO,ContextBuilder}.cs`, `unity/Assets/_Project/Scripts/AIClient/` (separate asmdef), `server/routers/behaviour.py`, `content/behaviour/intent_map.json`, tests.

## Dependencies
p1-ai-language-s01.

## Common issues
Hidden fact leaked → check `disclose_on` evaluation in DisclosableNow.

## Sources
Implementation Guide B-R5 Steps 1–6, T-RL1–T-RL6.
