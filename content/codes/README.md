# Standard terminology codes (p2-content-extensions-s03)

Extra columns of meaning beside our own ids. Nothing in the engine reads these files and a code never replaces one of our ids; they exist so exported results and diagnoses can be matched to the codes other systems use.

| File | Covers | Source |
|---|---|---|
| `loinc.csv` | every analyte of `content/lab_bands.csv` (23), with the unit our content uses | LOINC 2.82 |
| `icd11.csv` | every condition word of `content/ai/lexicon.json` (36): 30 codes and 6 words with no single code, each saying why | ICD-11 MMS release 2026-01 |

Checked on 18 October 2026 against the public HL7 terminology server (`tools/content/terminology_codes.py --verify`, which compares each code's display text). The choices were made by the builder and have **not** been reviewed by a clinician or terminologist; several ICD-11 codes can fit one word and the most general fitting code was used (the `note` column says where a more specific one exists).

`uv run --project tools tools/content/terminology_codes.py --check` (offline, in CI) tests the code formats (the LOINC check digit), that nothing is missing and that a word without a code says why.

## Notices

- LOINC: this material contains content from LOINC (http://loinc.org). LOINC is copyright Regenstrief Institute, Inc. and the Logical Observation Identifiers Names and Codes (LOINC) Committee and is available at no cost under the license at http://loinc.org/license. LOINC is a registered United States trademark of Regenstrief Institute, Inc.
- ICD-11: copyright World Health Organization, licensed CC BY-ND 3.0 IGO. Codes and titles are quoted unaltered; the notes are ours.
