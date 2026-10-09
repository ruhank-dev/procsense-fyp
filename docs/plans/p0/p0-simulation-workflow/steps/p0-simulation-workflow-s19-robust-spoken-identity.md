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
2. Word tables (day/ordinal words, month names, number words, honorifics, filler words) in a small `Language` class now; moved to `content/language/en.json` when a second language arrives (P2 Urdu — see p2-ai-extensions).
3. `IdentityCheck` uses `SpokenIdentity` for the spoken source; wristband and notes comparisons stay structural.
4. Date-format convention (`DD/MM` default) in content with a register row (institution decision, evidence type C/D).
5. Test matrix (table-driven, 40+ phrasings): the same patient said many ways → `Match`; wrong day, wrong month, wrong year, wrong surname, swapped given/family name → `Mismatch`; name only → `Missing`; "3/4/1984" → `Ambiguous`.
6. Property check: every phrasing in the matrix must give the same result after normalising twice (idempotent).
7. P1 hook: the same extractor is what `p1-voice-input` and `p1-ai-language` call on recognised or generated text (listed in their Dependencies).

## Verification
`dotnet test --filter SpokenIdentity` green; VAR-S1-A still verifies; `tests/golden/step1-identity` hash unchanged unless the answer text changed (record it if so).

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
