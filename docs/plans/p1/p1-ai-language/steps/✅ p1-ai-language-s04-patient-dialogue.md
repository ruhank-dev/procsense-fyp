# p1-ai-language-s04 — Patient free-text dialogue (EXP-AI-2)

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
Free questions answered in the patient's voice: topic first (menu or classifier) → **Core** decides disclosure → LLM only phrases; answer cache seeded with the walkthrough's quoted patient lines per case (LLM rephrases, never invents); first sentence streamed to TTS; output schema {utterance ≤ 240 chars, intent ANSWER | DEFLECT | ASK_BACK | EXPRESS_CONCERN, fact_ids_used[], emotion}.

## Why
Free questions make history-taking feel like a real conversation — but the walkthrough's hidden-fact mechanic and medical safety mean the LLM may only phrase what Core has decided to disclose.

## How to implement
1. `server/dialogue/patient.py`; prompt v1 per guide (first person, persona style, only context facts, say you don't know otherwise).
2. Persona style tags (terse / talkative / anxious-questioning / stoic) and health-literacy tag.
3. Eval loop with the adversarial set (100 questions × 4 archetypes); bump prompt version on change.
4. Unity client: question bubble, 'thinking' indicator, fallback to the menu on timeout.

## Status (17 October 2026)

- **Built:** `server/dialogue/dialogue.py` (`Dialogue.reply`) and `POST /dialogue/organisations/{id}/patient`: the Core's context (allowed intents, facts with the Core's decision `disclosable_now`) goes in; the model is shown only the facts marked disclosable, a closed set of intents and fact ids (schema enums), a style from the persona tags; the grounding validator checks the answer; one retry with the reasons; then the authored line of the asked fact or the approved `cannot_recall`; a cache seeded by the authored lines. Prompt `dialogue.patient`.
- **Tests:** a hidden fact is never in the prompt; a leaking model is refused twice and the fallback is used; an unshown fact id is refused by the schema; without a model the authored line is used; a repeat comes from the cache (`tools/ci/test_llm.py`, `server/tests/test_ai_routes.py`).
- **Measured** (`tools/ai/eval_dialogue.py`, qwen3.5:4b-q4_K_M, 12 cases): earned questions answered by the model 80 of 82 (fell back 2), cited the fact 67; withheld questions answered cannot_recall 74 of 82, validator leaks 0, shared-word leaks 0; latency p50 1870 ms, p95 2318 ms. Every withheld reply is in the result file for a person to read (DT-211 is the limit of the lexical check).

## Verification
T-PD1 0 invented facts; T-PD3 p95 ≤ 2.5 s; T-PD4 offline fallback; T-PD5/T-PD6 pass.

## Files changed
`server/routers/dialogue_patient.py`, `server/dialogue/patient.py`, `prompts/patient/v1.txt`, `server/schemas/patient_reply.py`, answer cache per case `content/dialogue_cache/<case>.json`, Unity `Scripts/AIClient/PatientDialogue.cs`.

## Dependencies
p1-ai-language-s03.

## Common issues
Patient repeats itself → include told_to_patient + last turns.

## Sources
Implementation Guide B-R3 Steps 1–6, T-PD1–T-PD6.
