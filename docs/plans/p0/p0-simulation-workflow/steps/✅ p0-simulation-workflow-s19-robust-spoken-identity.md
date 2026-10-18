# p0-simulation-workflow-s19 — Robust spoken identity validation

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

Added 09 October 2026 at the project owner's request ([deferred-tasks.md](../../../../deferred-tasks.md) DT-001, DT-026).

## What
Replace the shortcut in `IdentityCheck` (it looks for name + spelled-out day + month + year inside the patient's answer) with a proper **normaliser and extractor**
that reads names and dates correctly across different phrasings, and returns a clear result per field instead of one yes/no.

- **Normalise**: lower-case, trim, remove punctuation and filler words ("um", "my name is…", "it's"), fold accents, collapse spaces; ASR-style text with no punctuation works too.
- **Names**: strip honorifics (Mr/Mrs/Ms/Miss/Dr), accept "Khan, Ayesha" and "Ayesha Khan", ignore a middle name or initial, handle hyphens; compare as token sets, not substrings.
- **Dates of birth**: ordinal words (fourteenth), numeric ordinals (14th), digits (14/03/1984, 14-3-1984, 14.03.1984), full and short month names (Mar, Sept), "March 14, 1984",
  spoken years ("nineteen eighty-four", "two thousand and three"), "the fourteenth of March". 
- **Ambiguity is reported, never guessed**: a day ≤ 12 written numerically could be DD/MM or MM/DD; two-digit years are ambiguous. The result is `Ambiguous` and the date format
  convention comes from content (institution setting), not from code.
- **Result per field**: `Match`, `Mismatch`, `Missing` (the patient did not say it), `Ambiguous`. A name-only answer is `Missing` for the date, so the trainee asks again — not a false mismatch.
- **MRN** spoken as digits is parsed the same way when a case has the patient say it.

## Why
The current check works only because VAR-S1-A's answer is written in one exact style. More cases (and, in P1, speech recognition and AI-generated patient speech)
produce many phrasings; a wrong match or a false mismatch would silently break Step 1 for those cases.

## How to implement
1. `src/ProcSense.Core/Workflow/Steps/SpokenIdentity.cs`: `Normalise(text)`, `Extract(text) → SpokenFields { Name tokens, Date?, Mrn?, Flags }`, `Compare(extracted, identity) → FieldResult`.
   Pure functions, no culture-dependent calls (invariant culture, ordinal comparisons).
2. Word tables (day/ordinal words, month names, number words, honorifics, filler words) in a small `Language` class now; kept English only (owner decision, 18 Oct 2026: no second language).
3. `IdentityCheck` uses `SpokenIdentity` for the spoken source; wristband and notes comparisons stay structural.
4. Date-format convention (`DD/MM` default) in content with a register row (institution decision, evidence type C/D).
5. Test matrix (table-driven, 40+ phrasings): the same patient said many ways → `Match`; wrong day, wrong month, wrong year, wrong surname, swapped given/family name → `Mismatch`; name only → `Missing`; "3/4/1984" → `Ambiguous`.
6. Property check: every phrasing in the matrix must give the same result after normalising twice (idempotent).
7. P1 hook: the same extractor is what `p1-voice-input` and `p1-ai-language` call on recognised or generated text (listed in their Dependencies).

## Implementation notes (09 October 2026)
- Built as `SpokenIdentity.Normalise` / `Compare` (`Match`, `Mismatch`, `Missing`, `Ambiguous` for the name and for the date). Covers: honorifics and filler words, surname-first, middle names, accents, upper/lower case, digits and ordinals (14th), ordinal and cardinal day words (fourteenth, fourteen, twenty first), month names and abbreviations, ISO / day-first / month-first numeric dates, spoken years (nineteen eighty four, two thousand and three, twenty oh three), a name that is also a month word (June).
- **Ambiguity rule:** a numeric date is `Ambiguous` only when it could be her birthday under one reading but not the other (03/04 for 3 April), or has a two-digit year that fits. If neither reading is her birthday it is a plain `Mismatch`. A month word removes the doubt. Never guessed.
- **Change from the plan:** no date-format setting in `content/` (no new register row), because a date that depends on the order is reported as ambiguous instead of resolved by a convention.
- `confirm_identity` now rejects `SPOKEN_ANSWER_UNCLEAR` when her answer is incomplete or ambiguous (not a mismatch: the answer text needs fixing); `request_wristband` also requires an unambiguous answer. A wrong name or date is still `ST1_MISMATCH`.
- Names are matched by exact words (marked `shortcut` in the code); fuzzy matching comes with speech recognition (p1-voice-input, DT-032).
- 64 new tests: a 24-phrasing match matrix, 19 incomplete/wrong/ambiguous cases, ambiguity, spoken years, month-word names, idempotence, and four inside Step 1. Existing golden runs are unchanged.

## Verification
`dotnet test --filter SpokenIdentity` green; VAR-S1-A still verifies; the golden runs in `tests/golden` unchanged unless the answer text changed (record it if so).

## Files changed
`src/ProcSense.Core/Workflow/Steps/SpokenIdentity.cs`, `IdentityCheck.cs`, `content/` (date-format setting + register row), tests.

## Dependencies
p0-simulation-workflow-s01. Used later by p1-voice-input and p1-ai-language (grounding validator).

## Common issues
- Never "fix" an ambiguous date by guessing; report it and let the case author write an unambiguous answer.
- Do not accept a partial name as a full match (a first name alone is `Missing` surname).
- ASR output has no punctuation or capitals — normalise before any comparison.

## Sources
Implementation Guide §07 Step 1 (patient states name and DOB; compare ≥2 identifiers); Walkthrough Part 11 Step 1; owner instruction 09 October 2026.
