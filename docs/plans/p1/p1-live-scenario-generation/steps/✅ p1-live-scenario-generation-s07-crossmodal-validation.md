# p1-live-scenario-generation-s07 — Cross-modal consistency and Stage B on generated packages (LG-09, LG-10)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
Cross-modal rule table (VR-CM-*), Stage B and a headless correct-path run on each generated package.

## Why
No inconsistent or unsafe case reaches a trainee.

## How to implement
1. `content/generation/crossmodal_rules.json` with one pass and one fail fixture per rule.
2. Extend `tools/composer/mutate.py` with cross-modal breaks.
3. Headless run via the CLI.

## Status (17 October 2026)

- **Built:** `server/generation/crossmodal.py` (8 rules: ECG rhythm against the pulse, potassium against the ECG, scan against inflammatory markers, free air against the abdomen, a faulty sample against its repeat, lactate against the patient's state, a pregnancy test against sex and age, the order of times), the rule file `content/generation/crossmodal_rules.json` with the register rows behind each rule, and `server/generation/package.py` (`assemble`, `validate`): the gate every package passes: the package schema, the case against Stage B (`tools/composer`), the media check and the cross-modal rules; a package with any defect is not delivered.
- **Verification result** (`tools/ci/test_generation_crossmodal.py`, 13 tests): all 53 authored cases pass every rule with their own pictures; each rule has a failing example; assembled packages pass the gate; a break at each stage (schema, Stage B, media, cross-modal) is caught.
- **Not built:** Stage B checks 9 and 10 (idle run and the correct path through the engine) for a generated package: they need the engine to load a generated package (s10). The extended mutation set is the failing fixtures of the tests; the text consistency critic is s15.

## Verification
Every mutation caught; correct path reaches the declared end state.

## Files changed
`content/generation/crossmodal_rules.json`, `tools/composer/`

## Dependencies
p1-live-scenario-generation-s05, p1-live-scenario-generation-s06

## Common issues
Rule table drift: rules are content with `--check`.

## Sources
Spec LG-09, LG-10; A10, B18.
