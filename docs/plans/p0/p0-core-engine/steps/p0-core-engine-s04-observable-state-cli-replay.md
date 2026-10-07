# p0-core-engine-s04 — Observable state, headless CLI and replay

Walking skeleton 1 · step 5 of 15

## What
- **ObservableState** — the read-only snapshot of what the trainee may perceive (disclosed facts, identifiers visible on band/notes once read,
  vitals only for attached sensors, location, statuses, last rejection). Unity and AI read only this.
- **Headless CLI** `procsense-sim run --case VAR-S1-A --seed 42 --actions <file.json> --out result.json` → final state, observable,
  log, log hash.
- **Replay** `procsense-sim replay --log session.jsonl` → re-applies a recorded session and checks the hash.

## Why
The CLI is how the developer, CI and you test the whole simulation without a headset; replay is how a Quest session becomes a reproducible bug report.

## How to implement
1. `Observation/ObservableState.cs` (immutable record) + builder after each Apply/Tick; only fields the trainee could know.
2. `src/ProcSense.Cli`: `run` and `replay` commands (System.CommandLine or a small hand-rolled parser — keep dependencies minimal).
3. Action-script format (`*.actions.json`): list of `{ "t": simSeconds, "verb", "target", "params" }`.
4. Tests: replay of a run's own log reproduces the same hash; observable never contains undisclosed facts.
5. Taskfile: `task sim -- run --case VAR-S1-A --seed 42 --actions …`.

## Verification
`task sim -- run …` writes `result.json` and prints the hash; `task sim -- replay --log <that log>` prints "replay OK, hash match".

## Files changed
`src/ProcSense.Core/Observation/*.cs`, `src/ProcSense.Cli/Program.cs` (+ command files), tests, `Taskfile.yml`.

## Dependencies
p0-core-engine-s03.

## Common issues
- Keep the CLI thin: argument parsing + calling Core; no rules in the CLI.
- Write output with invariant culture and fixed key order (hash stability).

## Sources
Implementation Guide B-R11 Steps 7–8 (Observable snapshot, headless CLI), test T-K1 (replay).
