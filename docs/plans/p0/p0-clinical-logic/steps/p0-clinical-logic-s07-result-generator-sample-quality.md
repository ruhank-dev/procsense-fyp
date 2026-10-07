# p0-clinical-logic-s07 — Result generator and sample quality

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
Results come only from the case card (value, unit, flag, lab comment, previous values) with optional deterministic noise inside reviewed bands, plus sample-quality artefacts decided by how the sample was taken: HAEMOLYSED (false high K), DRIP_ARM (dilution / glucose), EDTA (false high K, low Ca), CLUMPED (false low platelets → repeat in citrate tube), MISLABELLED/REJECTED (re-take, incident).

## Why
Walkthrough Part 5: artefacts and repeat testing are core lessons (VAR-S1-J: K 6.4 haemolysed → repeat normal).

## How to implement
1. `Clinical/ResultGenerator.cs`: `Generate(case, test, sampleContext)` → Result{values[{analyte, value, unit, ref_low, ref_high, flag}], comment, quality, finding_codes[]}.
2. Own SimRandom stream 'results' (independent of physiology).
3. Artefact rules from `sample_quality.csv` (sample taken from drip arm, wrong tube order, delayed transport…).
4. Labelling: sample valid only if identity was confirmed in the same interaction; unlabelled → REJECTED event (T-IV3).
5. Safety: never emit a finding not in the case card (T-IV1).

## Verification
`dotnet test --filter FullyQualifiedName~ResultGenerator` → T-IV1, T-IV3, T-IV6 green; VAR-S1-J fixture: first K 6.4 haemolysed, repeat 4.6 (walkthrough values).

## Files changed
`src/ProcSense.Core/Clinical/ResultGenerator.cs`, tests.

## Dependencies
p0-clinical-logic-s06; p0-medical-research-data-s04.

## Common issues
Flags wrong → local range units mismatch (B-M13 troubleshooting).

## Sources
B-M13 Steps 3–4, 6, T-IV1/3/6; walkthrough Part 5; inventory D35, D162–D186.
