# p0-core-engine-s03 — Content loader and patient state

Walking skeleton 1 · step 4 of 15

## What
- **Content loader** — reads `content/` (case files, devices) into an immutable `ContentPackage`; validates against the schema rules;
  refuses unknown fields and missing register refs.
- **Patient state** — the first layers of the patient model: `Profile` (identity, wristband/notes identifiers, allergies), `Clinical`
  (identity_verified, allergies_known, revealed facts, location), `Physiological` (current vitals — from baseline for now),
  `Behavioural` (trust, anxiety — simple integers for now).

## Why
Every later rule reads and writes this state; values come only from content (never code).

## How to implement
1. `Content/ContentPackage.cs`, `Content/ContentLoader.cs` — JSON parsing with an explicit, AOT/IL2CPP-safe approach
   (System.Text.Json source-generated in the CLI build; a shared `IContentReader` interface so Unity can supply its own reader).
2. `State/PatientState.cs` with the four layers above; `State/PatientFactory.cs` builds state from a case card.
3. Disclosure model: each history fact has `revealed=false` until an `ask(topic)` reveals it (used in s02 of simulation-workflow).
4. Tests: load `VAR-S1-A.json` → profile fields equal the file; invalid file (extra field / missing ref) → load error with clear message.

## Verification
`task test` green; a test asserts no numeric literal for vitals exists in Core (values come from the loaded case).

## Files changed
`src/ProcSense.Core/Content/*.cs`, `src/ProcSense.Core/State/*.cs`, tests, test fixture copy of `VAR-S1-A.json` (or path to `content/`).

## Dependencies
p0-medical-research-data-s01 (case file), p0-core-engine-s02.

## Common issues
- Unity IL2CPP strips reflection-based JSON — keep parsing behind an interface; Unity side provides Newtonsoft + `link.xml` (B-R11 troubleshooting).
- Keep `refs` (register IDs) on the loaded objects — the debrief will show sources later.

## Sources
Implementation Guide B-R11 Step 1 + interface (ContentPackage), B-M11 Step 4 (loader); Master Plan §25 (PatientState layers).
