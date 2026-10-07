# p1-ai-language-s05 — Staff and environment AI

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
Staff utterances from approved intents per role with slots (e.g. nurse CONFIRM_ORDER, ASK_CLARIFICATION, REPORT_OBSERVATION, CHALLENGE 'Before I give this, can you confirm her allergy status?'); event communication intents (REPORT_DELAY, REPORT_FAULT…); every intent has a pre-rendered fallback line; extended roster voices (lab caller, radiologist, pharmacist, interpreter…) as voice-only.

## Why
Richer NPCs that can never invent instructions (T-AI-06).

## How to implement
1. Intent catalogue in content → schema enum generated (`gen_staff_enums.py`).
2. Prompts per role: professional, slots only, ≤ 2 sentences; temperature 0.3.
3. Nurse CHALLENGE intent wired to the medicine workflow (beginner mode) with slots {drug_class, reason}.
4. Event communication intents (REPORT_DELAY with minutes from slots, REPORT_FAULT with station) wired to the reaction layer.

## Verification
T-ST1–T-ST5 pass; every intent has a pre-rendered fallback line.

## Files changed
`server/routers/staff.py`, `prompts/staff/<role>/v1.txt`, `content/npc/intents.json`, `tools/content/gen_staff_enums.py`, pre-rendered fallback lines.

## Dependencies
p1-ai-language-s03.

## Common issues
Staff adds details → remove free fields from the schema; slots only.

## Sources
Implementation Guide B-R4 Steps 1–5, T-ST1–T-ST5.
