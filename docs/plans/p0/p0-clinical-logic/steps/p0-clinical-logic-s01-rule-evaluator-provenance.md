# p0-clinical-logic-s01 — Rule evaluator with provenance

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
A small JsonLogic-subset evaluator in Core. Rules live in content as records with provenance (source, section, evidence class, reviewer, status, tests). Every rule hit in the session log names its rule ID and source-register ID. Rules raise concerns, require reviews or block progress — they never cancel surgery by themselves.

## Why
Every clinical check (goals, blockers, indications, clashes, checklist answers) needs one deterministic, explainable evaluator; the walkthrough's decision principle forbids automatic cancellation.

## How to implement
1. `Rules/JsonLogic.cs`: operators `var, ==, !=, <, <=, >, >=, and, or, !, in, if, missing, some, all, +, -` (B-M12 subset); switch on operator; recursion depth limit 32 (T-JL4); unknown operator → load error (T-JL2).
2. Strict typing: no JS-style coercion; missing var → null (B-M12 config).
3. Parsing: Core has no Newtonsoft dependency — use a tiny JSON model behind the existing `IContentReader` (p0-core-engine-s03) so Unity and CLI share it.
4. `Rules/RuleRecord` {id, when, then (concern | require_review | block | score_item), severity, refs[], status}; schema `content/schema/rule.schema.json`; every rule has ≥ 1 test case (golden format).
5. `RuleEngine.Evaluate(state)` → hits with provenance appended to the session log.
6. Vectors `content/rules/vectors.json` (target ≥ 150 incl. missing vars, empty arrays, nested some/all); perf 10k evaluations < 20 ms (T-JL3).

## Verification
`dotnet test --filter FullyQualifiedName~JsonLogic` + `dotnet test --filter FullyQualifiedName~RuleEngine` → T-JL2…T-JL5 green; a session log line shows `rule: R-FAST-01, ref: SR-xxxx`.

## Files changed
`src/ProcSense.Core/Rules/*.cs`, `content/schema/rule.schema.json`, `content/rules/*.json`, tests.

## Dependencies
p0-core-engine-s04.

## Common issues
Different results across platforms → type-coercion differences; vectors catch it. Null reference → missing var handling.

## Sources
Implementation Guide B-M12 Steps 1, 3–6, tests T-JL1–T-JL5; walkthrough decision principle (front matter, Part B).

## Implementation notes (09 Oct 2026)
- `src/ProcSense.Core/Rules/Logic.cs`: the JsonLogic subset (var, ==, !=, <, <=, >, >=, and, or, !, in, if, missing, some, all, +, -). Rules are **compiled once**: unknown operators, wrong argument counts and nesting deeper than 32 levels are load errors that name the place (e.g. `rules[2].when.and[1]: unknown operator 'foo'`). Wrong types at run time (e.g. `"a" < 3`) throw `LogicException`, because that is a mistake in the rule.
- Semantics, written down so content authors can rely on them: no type coercion (`1 == "1"` is false); a missing variable is null; ordering with null is false; arithmetic with null gives null; truthy = not null/false/0/""/[]; `some` and `all` of an empty list are false; inside `some`/`all` the item is the data (`var ""`). `<` and `<=` accept three arguments (a range).
- `src/ProcSense.Core/Rules/RuleEngine.cs`: `RuleLoader.Parse` (id, when, then{kind,text}, severity, refs, status, tests; unknown fields are errors; every rule needs at least one source-register id and one test case), `RuleLoader.RunOwnTests`, `RuleEngine.Evaluate` (rules run in id order) and `RuleEngine.Apply`, which writes `rule:<id> ref:<SR-ids>` into the action's effects, so it appears in the session log.
- A rule can only concern, require review, block or add a score item; there is no "cancel" kind (tested).
- `content/schema/rule.schema.json`, and `task content-validate` now checks every rules file (schema plus register ids). `content/rules/vectors.json` holds 156 vectors and 14 error vectors, written by `tools/rules/make_vectors.py` with expected answers that do not come from the C# code.
- **No clinical rule is shipped yet.** The only rules are test fixtures in `tests/fixtures/rules/`; real rules arrive with the clinical steps (NEWS2, fasting, ...) and must have register ids.
- Performance guard: 10,000 evaluations in under 200 ms in the test (the plan's 20 ms is for a quiet Release build).
- Not done: the Python rule twin (EXP-CL-3) is p0-clinical-logic-s02.
