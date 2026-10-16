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

## Implementation notes (16 Oct 2026)
- **Built:** `tools/voice/catalogue.py` (`task voice-catalogue`, run by `task content-refresh`, checked in CI) writes `content/voice/catalogue.json` from the content only: 300 lines for the 17 P0 cases, each with an id (for example `PAT-VAR-S1-A-IDENTITY`), the speaker, the words, the Core event that plays it and its source. The patient's lines are the answers to the identity, allergy, allergy-reaction, reason, responsible-adult and 12 history questions (the case card when there is one, else the case file, as the engine reads them); the staff lines are the anaesthetist's 9 approved intents, the clinician's and nurse's 7 medicine lines, the nurse's prompt about tubes at the bedside, the porter's delay message and the team's answers to the WHO checklist items they answer. 9 lines are templates with values filled in (`{treatment}`, `{asa}` and so on), marked as such.
- **Authored variants:** `content/voice/spoken_variants.json` holds 14 first-person drafts where the content holds a narration or no words (9 "Not applicable." pregnancy answers, the penicillin reaction, VAR-S1-A's responsible adult, 3 checklist answers that need no case fact), each `awaiting_review`; the catalogue uses the draft and keeps the source text beside it.
- **Events without a line (T-VP1: 19, listed by the check, not invented):** the responsible-adult answer of 16 cases (the case files do not say who takes the patient home: a fact, not wording, DT-178); the surgeon's and anaesthetist's Time Out answers, which are case-specific clinical content (DT-179); the laboratory's critical-result call, whose words are built in the engine's code rather than content (DT-180). The step's verification ("0 events without a line") is therefore not met yet.
- **Not built:** the review of the 14 drafts (DT-181); rendering the lines to audio is p0-voice-audio-s02.
- **Approved by the owner on 16 Oct 2026** (decision row 90); the step file stays unticked until the 19 events have lines and the drafts are reviewed.
