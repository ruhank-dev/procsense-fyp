# p0-simulation-workflow-s11 — Step 9: consent

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Consent discussion as required elements (diagnosis, procedure, benefits, risks with numbers, alternatives, what happens if nothing is done), patient questions from the case pool, teach-back, capacity / interpreter / minor / refusal checks, and signing rules.

## Why
Walkthrough Step 9 + Part 10: invalid consent is a blocker; refusal is respected.

## How to implement
1. Verbs `consent_discussion(topic)`, `answer_question(q, a)`, `teach_back`, `check_capacity`, `request_interpreter`, `sign_consent`.
2. L9-01: required topics from `content/consent.json` (per procedure); ticks only in practice UI.
3. L9-02: signature accepted only with elements complete + understanding ≥ threshold (practice) / logged otherwise (assessment).
4. L9-03: approved answers raise trust; false reassurance → communication item (E9-B).
5. Capacity false (confused patient) → consent invalid → safety failure if signed; interpreter must be trained (not a child relative); minor → guardian per institutional rule (decision 3); refusal respected (pressuring = professionalism failure).
6. Transfer to theatre without valid consent → blocked (T09-3).

## Verification
`dotnet test --filter FullyQualifiedName~Consent` → T09-1…T09-3 green; VAR-S1-A teach-back line from walkthrough Scene 8 reproduced; skipping risks → major item.

## Files changed
`src/ProcSense.Core/Workflow/Steps/ConsentSubsystem.cs`, `content/consent.json`, tests.

## Dependencies
p0-simulation-workflow-s05; p0-physiology-events-behaviour-s10 (capacity flag, understanding).

## Common issues
Complication figures are unit-specific (institutional decision 14) — from content with their source.

## Sources
Implementation Guide §15 (L9-01…L9-03, E9-A/B, T09-1…T09-3); walkthrough Part 10, Step 9, Part 12 Scene 8.
