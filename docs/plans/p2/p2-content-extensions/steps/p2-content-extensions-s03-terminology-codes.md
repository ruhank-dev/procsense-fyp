# p2-content-extensions-s03 — Terminology codes (ICD-11, LOINC)

Milestone: **M13 P2 extensions**

## What
Add standard codes alongside our IDs: ICD-11 codes for every diagnosis and comorbidity in content, LOINC codes for every lab test — as extra columns, with the date checked, validated for format.

## Why
Standard codes make content interoperable (e.g. exporting results), give stable external meanings, and strengthen the proof-of-truth trail.

## How to implement
1. Mapping tables; validator checks code format.
2. ICD-11: map each diagnosis/comorbidity in content to an ICD-11 code via the WHO ICD-11 browser (record code + date).
3. LOINC: register (free), map each lab test to a LOINC code; record terms.
4. Validator checks code format; codes never replace our IDs.
5. Export option: results/diagnoses in the session report can include codes.

## Verification
Mapping coverage 100% for P0 diagnoses/tests; validator green.

## Files changed
`content/codes/icd11.csv`, `content/codes/loinc.csv`, validator rule in `tools/content/checks.py`, register notes.

## Dependencies
p0-medical-research-data-s04.

## Common issues
Several ICD-11 codes can fit one diagnosis — choose the most specific and have the reviewer confirm; LOINC needs a free account.

## Sources
Inventory D29 (WHO ICD-11), D30 (LOINC).
