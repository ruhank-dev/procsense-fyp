# p0-scoring-debrief-s04 — DebriefFacts, template debrief and timeline

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Structured `DebriefFacts` (schema `debrief_facts.v1`): each fact with log refs, rule ID, source; template text per item type from `content/debrief/templates.json` (e.g. "You did not state {allergy} at handover (critical). Rule {rule_id} [{source}]."); a timeline of key events, decisions and lateness; what-ifs encountered; stacked-case debrief block from Part D §20.

## Why
Learning happens in the debrief; templates work offline and cannot invent mistakes. The same facts feed the AI debrief in P1.

## How to implement
1. `Scoring/DebriefFacts.cs` exporter; JSON schema in `content/schema/debrief_facts.schema.json` (T-SCO5).
2. Template renderer with placeholders only from facts.
3. Timeline builder from the session log (sim times, key actions, events, lateness).
4. Test: every debrief sentence links to ≥ 1 log entry.
5. Stacked cases use the Part D §20 debrief block; variants use the walkthrough's teaching point text.

## Verification
`dotnet test --filter FullyQualifiedName~Debrief` → T-SCO1, T-SCO5 green.

## Files changed
`src/ProcSense.Core/Scoring/Debrief*.cs`, `content/debrief/templates.json`, schema, tests.

## Dependencies
p0-scoring-debrief-s02, s03.

## Common issues
Keep facts complete enough for the AI rewrite later (spans, sources).

## Sources
B-M15 Steps 4–5, B-M3 Step 5, B-S5 Step 5; walkthrough Part D §20.
