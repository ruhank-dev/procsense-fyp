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

## Status (17 October 2026)

- **Built (18 October 2026):** `content/codes/loinc.csv` (a LOINC code, display, unit and check date for each of the 23 analytes of `content/lab_bands.csv`) and `content/codes/icd11.csv` (an ICD-11 MMS code for 30 of the 36 condition words of `content/ai/lexicon.json`; the other 6 say why no single code fits), `tools/content/terminology_codes.py` (`--check` in CI: code formats including the LOINC check digit, coverage, a stated reason where there is no code; `--verify`: each code looked up on the public HL7 terminology server and its display compared; all 53 matched on 18 October) and `tools/ci/test_terminology_codes.py`. The licence notices are in `content/codes/README.md`.
- **Not done:** a clinician or terminologist has not reviewed the choices (the most general fitting ICD-11 code was used; the note says where a more specific one exists); the session report does not yet print the codes (step 5, an optional export); the imaging studies and the panels (full blood count, urea and electrolytes) have no code here because the table covers analytes. The coverage figure is therefore for analytes and condition words, not for every diagnosis of every case file: the case problem labels are sentences, not diagnoses, and mapping them needs a clinician (DT-218).

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
