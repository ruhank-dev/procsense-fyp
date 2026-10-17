# p1-live-scenario-generation-s12 — Grounded debrief, history and the offline feedback loop (LG-19 to LG-21)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
Debrief cites the generated package's facts; history stores fingerprint and package hash; the offline evaluation report proposes pack, prompt and threshold changes for a reviewed release.

## Why
Closes the loop without ever retraining live.

## How to implement
1. Extend DebriefFacts with package provenance.
2. `tools/generation/evaluate.py`.

## Status (17 October 2026)

- **Built:** the report of a generated session carries a `generated` section (spine, status, tier, package hash; schema and page updated) and the engine history keeps the tier of a generated case (`SessionRecord.Tier`); `server/generation/history.py` (`entry_from_report`, `FileHistory` with the privacy erase, `session_records`, `novelty_history`, `recent_fingerprints`: a pseudonym, the package id and hash, the story, the fingerprint levels, the tier, credits and penalties per behaviour, UTC time, no readable name); `tools/generation/evaluate.py` (a simulated batch or a request log in, a report with a stable id, proposals with their numbers out) and `content/generation/releases.json` with a check that every release cites an existing report and the packs match the latest release (R1 cites EVAL-7603eb25344c).
- **Verification result** (`tools/ci/test_generation_history.py`, 12 tests): the generated report is valid against the schema and its page says it is a generated practice case; a library report has no such section; the history entry holds no readable name and refuses a name for a pseudonym, a missing package or the wrong package; the store round-trips and forgets; the next request leaves out the story just played; the report id is stable; proposals follow the numbers; changed packs without a release are caught; nothing in server/ imports the loop.
- **What the first batches found:** with ratings, 15 percent of requests ended in a fallback because every story failed the coverage gate for a dimension nothing in the family exercises. Coverage is now advisory and reported, the ranking prefers stories that exercise more of the requested dimensions, and the 240-request batch has no fallbacks; the report asks for stories that exercise the dimensions most often missed (information to clarify, competing priorities, cognitive workload) and notes that only 17 of 51 approved stories were used.
- **Not built:** the language-model debrief wording (DT-202); the backend table and the call that writes history when a session ends (DT-201); a language-model summary of the proposals. The loop runs offline only.

## Verification
Every change to packs or prompts cites an evaluation report.

## Files changed
`tools/generation/evaluate.py`

## Dependencies
p1-live-scenario-generation-s11, p1-ai-language-s08

## Common issues
Feedback loop must not run inside sessions.

## Sources
Spec LG-19 to LG-21; E51.
