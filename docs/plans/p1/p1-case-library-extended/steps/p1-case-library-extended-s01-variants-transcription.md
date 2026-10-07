# p1-case-library-extended-s01 — Transcribe the 10 remaining variants

Milestone: **M11 P1 content — theatre extension, extended cases, composer, Pulse**

## What
Case files for VAR-S1-I (penicillin anaphylaxis 2015), -K (Hb 98, MCV 72), -L (new AF HR 128), -M (BMI 41, STOP-BANG 6, ASA 3) and VAR-S2-E (weight loss, CT first), -H (pregnant 20 weeks: maternity tool, US then MRI, left tilt), -I (16-year-old: minor consent per law), -J (dysuria, don't stop at UTI), -K (childhood penicillin rash), -L (capacitous refusal; antibiotics-only option).

## Why
The ten remaining variants complete the walkthrough's variant set (Parts 12–13), each teaching a specific point (e.g. anaemia, new AF, obesity/airway, penicillin allergy, pregnancy, minor consent, capacitous refusal).

## How to implement
1. Same process as P0 case transcription (walkthrough Parts 12–13; inventory D459–D474 as checklist).
2. Verbatim check 100%; register rows.
3. Variant-specific rules recorded as content (e.g. maternity early-warning placeholder, minor consent per institution).

## Verification
Validator + Stage B green; verbatim 100% per case.

## Files changed
`content/cases/VAR-S1-{I,K,L,M}.json`, `content/cases/VAR-S2-{E,H,I,J,K,L}.json`, register rows, verbatim-check output.

## Dependencies
p0-case-library-s01.

## Common issues
Two different 'Mr Imran' patients and other ID collisions (WT-I01) — check namespaced IDs.

## Sources
Walkthrough Parts 12–13; inventory D459–D474; Change Report WT-I01.
