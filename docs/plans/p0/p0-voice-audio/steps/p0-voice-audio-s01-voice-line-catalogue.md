# p0-voice-audio-s01 — Voice-line catalogue

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

## What
Every spoken line with an ID (e.g. `PAT-VAR-S1-A-IDENTITY`), speaker, text, the Core event that plays it, and its content source — generated from case files (patient answers), approved staff intents and checklist NPC answers.

## Why
Offline voices need a fixed, traceable list; no line without a source.

## How to implement
1. `tools/voice/catalogue.py` builds `content/voice/catalogue.json` from content (no hand duplication).
2. Missing line for an event → build warning (T-VP1).
3. First-person spoken variants for walkthrough narration answers are drafted by the developer, flagged `spoken_variant: authored` and sent for review.
4. Staff lines come from approved intents; checklist NPC answers from content.

## Verification
`task voice-catalogue` builds the catalogue; it reports 0 events without a line for P0 cases and lists authored variants awaiting review.

## Files changed
`tools/voice/catalogue.py`, `content/voice/catalogue.json` (generated), `content/voice/spoken_variants.json` (authored, review status).

## Dependencies
p0-medical-research-data-s10–s12.

## Common issues
Lines in two places drift apart → the catalogue is generated from content only, never edited by hand.

## Sources
Implementation Guide B-M9 Step 4.
